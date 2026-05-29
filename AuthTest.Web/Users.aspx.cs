using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.UI;

namespace AuthTest.Web
{
    public partial class UsersPage : Page
    {
        protected async void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                if (!SessionHelper.IsAuthenticated)
                {
                    Response.Redirect("Login.aspx");
                    return;
                }

                if (!SessionHelper.IsEnrolled)
                {
                    Response.Redirect("Enroll.aspx");
                    return;
                }

                lblCurrentUser.Text = "Connecté en tant que: " + SessionHelper.CurrentUsername;

                try
                {
                    await LoadUsersAsync();
                }
                catch (Exception ex)
                {
                    lblError.Text = "Erreur lors du chargement des utilisateurs: " + ex.Message;
                }
            }
        }

        private async Task LoadUsersAsync()
        {
            try
            {
                var users = await ApiClient.GetAsync<List<Dictionary<string, object>>>("users");

                gvUsers.DataSource = users;
                gvUsers.DataBind();
            }
            catch (Exception ex)
            {
                lblError.Text = "Erreur: " + ex.Message;
            }
        }

        protected void btnLogout_Click(object sender, EventArgs e)
        {
            SessionHelper.Clear();
            Response.Redirect("Login.aspx");
        }
    }
}
