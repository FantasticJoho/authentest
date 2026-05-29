using System;

namespace AuthTest.Web
{
    public partial class Default : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.IsAuthenticated)
                Response.Redirect("Login.aspx");
            else if (!SessionHelper.IsEnrolled)
                Response.Redirect("Enroll.aspx");
            else
                Response.Redirect("Users.aspx");
        }
    }
}
