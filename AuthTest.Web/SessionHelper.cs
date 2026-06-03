using System.Web;

namespace AuthTest.Web
{
    public static class SessionHelper
    {
        public static string CurrentUsername
        {
            get => HttpContext.Current.Session["Username"] as string;
            set => HttpContext.Current.Session["Username"] = value;
        }

        public static bool IsEnrolled
        {
            get => HttpContext.Current.Session["Enrolled"] as bool? ?? false;
            set => HttpContext.Current.Session["Enrolled"] = value;
        }

        public static bool IsAuthenticated => !string.IsNullOrEmpty(CurrentUsername);

        public static void Clear()
        {
            HttpContext.Current.Session.Clear();
        }
    }
}
