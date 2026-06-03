using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Web.UI;

namespace AuthTest.Web
{
    public partial class LoginPage : Page
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack && SessionHelper.IsAuthenticated)
            {
                if (!SessionHelper.IsEnrolled) Response.Redirect("Enroll.aspx");
                else Response.Redirect("Users.aspx");
            }
        }

        protected async void btnCheckUsername_Click(object sender, EventArgs e)
        {
            await CheckUsernameAsync();
        }

        private async Task CheckUsernameAsync()
        {
            var username = txtUsername.Text.Trim();
            if (string.IsNullOrEmpty(username)) { lblError.Text = "Entrez un nom d'utilisateur."; return; }

            var result = await ApiClient.PostAsync<Dictionary<string, object>>("auth/check", new { username });

            bool exists = result.ContainsKey("exists") && (bool)result["exists"];
            if (!exists) { lblError.Text = "Utilisateur introuvable."; return; }

            bool hasWebAuthn = result.ContainsKey("hasWebAuthn") && (bool)result["hasWebAuthn"];
            SessionHelper.CurrentUsername = username;

            pnlUsername.Visible = false;
            if (hasWebAuthn)
            {
                var rpId = Request.Headers["Host"]?.Split(':')[0] ?? Request.Url?.Host;
                var options = await ApiClient.PostAsync<object>("webauthn/authenticate/begin", new { username, rpId });
                hdnAssertionOptions.Value = Json.Serialize(options);
                pnlWebAuthn.Visible = true;
            }
            else
            {
                pnlPassword.Visible = true;
            }
        }

        protected async void btnLogin_Click(object sender, EventArgs e)
        {
            await LoginAsync();
        }

        private async Task LoginAsync()
        {
            var username = SessionHelper.CurrentUsername;
            var password = txtPassword.Text;
            if (string.IsNullOrEmpty(username)) { Response.Redirect("Login.aspx"); return; }

            var result = await ApiClient.PostAsync<Dictionary<string, object>>("auth/login", new { username, password });

            bool success = result.ContainsKey("success") && (bool)result["success"];
            if (!success) { lblError.Text = result.ContainsKey("error") ? result["error"]?.ToString() : "Erreur de connexion."; pnlPassword.Visible = true; return; }

            bool mustChange = result.ContainsKey("mustChangePassword") && (bool)result["mustChangePassword"];
            if (mustChange) Response.Redirect("ChangePassword.aspx");
            else Response.Redirect("Enroll.aspx");
        }

        protected async void btnWebAuthnComplete_Click(object sender, EventArgs e)
        {
            await WebAuthnCompleteAsync();
        }

        private async Task WebAuthnCompleteAsync()
        {
            var username = SessionHelper.CurrentUsername;
            var assertionJson = hdnAssertionResponse.Value;
            if (string.IsNullOrEmpty(assertionJson)) { lblError.Text = "Réponse WebAuthn manquante."; pnlWebAuthn.Visible = true; return; }

            try
            {
                var rpId = Request.Headers["Host"]?.Split(':')[0] ?? Request.Url?.Host;
                var assertionResponse = Json.Deserialize<object>(assertionJson);
                var result = await ApiClient.PostAsync<Dictionary<string, object>>("webauthn/authenticate/complete", new { username, assertionResponse, rpId });

                if (result == null) { lblError.Text = "Réponse vide du serveur."; pnlWebAuthn.Visible = true; return; }

                bool success = result.ContainsKey("success") && result["success"] is bool b && b;
                if (!success)
                {
                    string errMsg = result.ContainsKey("error") ? result["error"]?.ToString()
                        : result.ContainsKey("title") ? result["title"]?.ToString()
                        : "Échec WebAuthn.";
                    lblError.Text = errMsg;
                    pnlWebAuthn.Visible = true;
                    return;
                }

                SessionHelper.IsEnrolled = true;
                Response.Redirect("Users.aspx");
            }
            catch (Exception ex)
            {
                lblError.Text = "Erreur : " + ex.Message;
                pnlWebAuthn.Visible = true;
            }
        }
    }
}
