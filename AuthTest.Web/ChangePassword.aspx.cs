using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.UI;

namespace AuthTest.Web
{
    public partial class ChangePasswordPage : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack && !SessionHelper.IsAuthenticated)
            {
                Response.Redirect("Login.aspx");
            }
        }

        protected async void btnChangePassword_Click(object sender, EventArgs e)
        {
            await ChangePasswordAsync();
        }

        private async Task ChangePasswordAsync()
        {
            var newPassword = txtNewPassword.Text;
            var confirmPassword = txtConfirmPassword.Text;
            var token = SessionHelper.SessionToken;

            if (string.IsNullOrWhiteSpace(token))
            {
                Response.Redirect("Login.aspx");
                return;
            }

            if (string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            {
                lblMessage.Text = "Veuillez remplir tous les champs.";
                lblMessage.CssClass = "error";
                return;
            }

            if (newPassword != confirmPassword)
            {
                lblMessage.Text = "Les mots de passe ne correspondent pas.";
                lblMessage.CssClass = "error";
                return;
            }

            if (newPassword.Length < 8)
            {
                lblMessage.Text = "Le mot de passe doit contenir au moins 8 caractères.";
                lblMessage.CssClass = "error";
                return;
            }

            var result = await ApiClient.PostAsync<Dictionary<string, object>>("auth/change-password", new { token, newPassword });

            bool success = result.ContainsKey("success") && (bool)result["success"];
            if (!success)
            {
                lblMessage.Text = result.ContainsKey("error") ? result["error"]?.ToString() : "Erreur lors du changement de mot de passe.";
                lblMessage.CssClass = "error";
                return;
            }

            lblMessage.Text = "Mot de passe changé avec succès. Redirection...";
            lblMessage.CssClass = "success";
            SessionHelper.IsEnrolled = false;
            Response.Redirect("Enroll.aspx");
        }
    }
}
