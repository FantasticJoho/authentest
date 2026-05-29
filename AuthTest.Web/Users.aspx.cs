using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace AuthTest.Web
{
    public partial class UsersPage : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.IsAuthenticated) { Response.Redirect("Login.aspx"); return; }
            if (!SessionHelper.IsEnrolled) { Response.Redirect("Enroll.aspx"); return; }

            if (!IsPostBack)
            {
                lblCurrentUser.Text = "Connecté en tant que : " + SessionHelper.CurrentUsername;
                RegisterAsyncTask(new PageAsyncTask(LoadUsersAsync));
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
                lblError.Text = "Erreur chargement : " + ex.Message;
            }
        }

        protected async void gvUsers_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            var id = e.CommandArgument?.ToString();
            if (string.IsNullOrEmpty(id)) return;

            try
            {
                if (e.CommandName == "Reset2FA")
                {
                    var result = await ApiClient.PostAsync<Dictionary<string, object>>($"users/{id}/reset-2fa", new { });
                    bool ok = result.ContainsKey("success") && (bool)result["success"];
                    lblMessage.CssClass = ok ? "success" : "error";
                    lblMessage.Text = ok ? "2FA réinitialisé." : "Erreur reset 2FA.";
                }
                else if (e.CommandName == "DeleteUser")
                {
                    var resp = await ApiClient.DeleteAsync($"users/{id}");
                    lblMessage.CssClass = resp.IsSuccessStatusCode ? "success" : "error";
                    lblMessage.Text = resp.IsSuccessStatusCode ? "Utilisateur supprimé." : "Erreur suppression.";
                }

                RegisterAsyncTask(new PageAsyncTask(LoadUsersAsync));
            }
            catch (Exception ex)
            {
                lblError.Text = "Erreur : " + ex.Message;
            }
        }

        protected void btnLogout_Click(object sender, EventArgs e)
        {
            SessionHelper.Clear();
            Response.Redirect("Login.aspx");
        }
    }
}
