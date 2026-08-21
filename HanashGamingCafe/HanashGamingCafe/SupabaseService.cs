using System;
using System.Threading.Tasks;
using Supabase;

namespace HanashGamingCafe
{
    public static class SupabaseService
    {
        // 🟢 الاتصال الأساسي المباشر بقاعدة البيانات
        private static readonly string SupabaseUrl = "https://iihcevuyrdoezfozhots.supabase.co";
        private static readonly string SupabaseKey = "sb_publishable_jGD6jV7XZr2H8oXVKxxP4Q_IBI6eNwE";

        public static Client Instance { get; private set; }

        public static async Task InitializeAsync()
        {
            if (Instance == null)
            {
                var options = new SupabaseOptions
                {
                    AutoConnectRealtime = true
                };

                Instance = new Client(SupabaseUrl, SupabaseKey, options);
                await Instance.InitializeAsync();
            }
        }
    }
}