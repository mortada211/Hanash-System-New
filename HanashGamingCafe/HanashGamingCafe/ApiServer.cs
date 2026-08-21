using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HanashGamingCafe;

namespace HanashGamingCafe
{
    public static class ApiServer
    {
        private static HttpListener _listener;
        private static bool _isRunning = false;

        // 🚀 تشغيل الخادم
        public static void StartServer()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://localhost:8080/api/");
                _listener.Prefixes.Add("http://127.0.0.1:8080/api/");
                _listener.Start();
                _isRunning = true;

                Thread serverThread = new Thread(Listen)
                {
                    IsBackground = true
                };
                serverThread.Start();

                Console.WriteLine("تم تشغيل خادم الـ API بنجاح على البورت 8080...");
            }
            catch (Exception ex)
            {
                MessageBox.Show("خطأ أثناء تشغيل خادم الـ API: " + ex.Message);
            }
        }

        // 🎧 الاستماع للطلبات القادمة من الآيباد
        private static async void Listen()
        {
            while (_isRunning)
            {
                try
                {
                    HttpListenerContext context = _listener.GetContext();
                    HttpListenerRequest request = context.Request;
                    HttpListenerResponse response = context.Response;

                    // للسماح بالاتصال من الأجهزة المختلفة (CORS)
                    response.Headers.Add("Access-Control-Allow-Origin", "*");
                    response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                    response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                    if (request.HttpMethod == "OPTIONS")
                    {
                        response.StatusCode = 200;
                        response.Close();
                        continue;
                    }

                    string responseString = "";

                    // 1️⃣ طلب جلب قائمة الأجهزة والطاولات
                    if (request.Url.AbsolutePath.EndsWith("/devices") && request.HttpMethod == "GET")
                    {
                        responseString = @"{""status"": ""success""}";
                    }
                    // 2️⃣ طلب إضافة طلب جديد من الآيباد
                    else if (request.Url.AbsolutePath.EndsWith("/add-order") && request.HttpMethod == "POST")
                    {
                        using (StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding))
                        {
                            string jsonReceived = reader.ReadToEnd();
                            Console.WriteLine("وصل طلب جديد من الآيباد: " + jsonReceived);

                            try
                            {
                                // 🟢 قراءة البيانات القادمة من الآيباد
                                using var doc = JsonDocument.Parse(jsonReceived);
                                var root = doc.RootElement;

                                string itemName = root.GetProperty("item_name").GetString();
                                double price = root.GetProperty("price").GetDouble();
                                // 🟢 قراءة الكمية بأمان حتى لو أرسلها الآيباد كـ double أو string مثل "1.00"
                                int qty = 1;
                                if (root.TryGetProperty("qty", out var qtyElement))
                                {
                                    if (qtyElement.ValueKind == JsonValueKind.Number)
                                    {
                                        qty = Convert.ToInt32(qtyElement.GetDouble());
                                    }
                                    else if (qtyElement.ValueKind == JsonValueKind.String)
                                    {
                                        if (double.TryParse(qtyElement.GetString(), out double parsedDouble))
                                        {
                                            qty = Convert.ToInt32(parsedDouble);
                                        }
                                    }
                                }

                                // 🟢 جلب الـ ID الحقيقي للجلسة المحددة حالياً في شاشة الكاشير
                                string realSessionId = "";

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    if (Application.Current.MainWindow is MainWindow mainWindow)
                                    {
                                        realSessionId = mainWindow._selectedSession?.Id;
                                    }
                                });

                                // التحقق من وجود جلسة نشطة ومحددة
                                if (!string.IsNullOrEmpty(realSessionId))
                                {
                                    // 🟢 1. حفظ الطلب بـ Supabase باستخدام الكلاس المستقل
                                    var newOrder = new SessionOrderDatabaseModel
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        SessionId = realSessionId,
                                        ItemName = itemName,
                                        Price = price,
                                        Qty = qty,
                                        TotalPrice = qty * price
                                    };

                                    await SupabaseService.Instance
                                        .From<SessionOrderDatabaseModel>()
                                        .Insert(newOrder);

                                    // 🟢 2. أمر شاشة الكاشير الرئيسية بالتحديث الفوري
                                    await Application.Current.Dispatcher.Invoke(async () =>
                                    {
                                        if (Application.Current.MainWindow is MainWindow mainWindow)
                                        {
                                            await mainWindow.SyncSessionOrdersFromSupabaseAsync(realSessionId);
                                            mainWindow.RecalculateSelectedSessionBill();
                                        }
                                    });

                                    responseString = @"{""success"": true, ""message"": ""تم حفظ الطلب وتحديث الكاشير بنجاح!""}";
                                }
                                else
                                {
                                    responseString = @"{""success"": false, ""error"": ""لم يتم تحديد أي جلسة في الكاشير حالياً!""}";
                                }
                            }
                            catch (Exception ex)
                            {
                                responseString = $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
                            }
                        }
                    }
                    else
                    {
                        responseString = @"{""error"": ""Endpoint not found""}";
                    }

                    // إرسال الرد للآيباد
                    byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "application/json; charset=utf-8";
                    Stream output = response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);
                    output.Close();
                }
                catch (Exception)
                {
                    // في حال إيقاف السيرفر
                }
            }
        }

        // 🛑 إيقاف الخادم عند إغلاق الكاشير
        public static void StopServer()
        {
            _isRunning = false;
            _listener?.Stop();
        }
    }
}