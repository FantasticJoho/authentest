using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Web.UI;

namespace AuthTest.Web
{
    public partial class EnrollPage : Page
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.IsAuthenticated) { Response.Redirect("Login.aspx"); return; }
            if (SessionHelper.IsEnrolled) { Response.Redirect("Users.aspx"); return; }

            if (!IsPostBack)
            {
                RegisterAsyncTask(new PageAsyncTask(LoadOptionsAsync));
            }
        }

        private async Task LoadOptionsAsync()
        {
            try
            {
                var username = SessionHelper.CurrentUsername;
                if (string.IsNullOrWhiteSpace(username)) { Response.Redirect("Login.aspx"); return; }

                var rpId = Request.Headers["Host"]?.Split(':')[0] ?? Request.Url?.Host;
                var options = await ApiClient.PostAsync<object>("webauthn/register/begin",
                    new { username, rpId });
                hdnCreationOptions.Value = Json.Serialize(options);
            }
            catch (Exception ex)
            {
                lblRegisterError.Text = "Erreur chargement options : " + ex.Message;
            }
        }

        protected async void btnRegisterComplete_Click(object sender, EventArgs e)
        {
            await RegisterCompleteAsync();
        }

        private async Task RegisterCompleteAsync()
        {
            var username = SessionHelper.CurrentUsername;
            if (string.IsNullOrWhiteSpace(username)) { Response.Redirect("Login.aspx"); return; }

            var keyName = txtKeyName.Text?.Trim();
            if (string.IsNullOrWhiteSpace(keyName)) { lblRegisterError.Text = "Le nom de clé est obligatoire."; return; }

            var attestationJson = hdnAttestationResponse.Value;
            if (string.IsNullOrEmpty(attestationJson)) { lblRegisterError.Text = "Réponse WebAuthn manquante."; return; }

            try
            {
                var rpId = Request.Headers["Host"]?.Split(':')[0] ?? Request.Url?.Host;
                var attestationResponse = Json.Deserialize<object>(attestationJson);
                var result = await ApiClient.PostAsync<Dictionary<string, object>>("webauthn/register/complete",
                    new { username, keyName, attestationResponse, rpId });

                bool success = result.ContainsKey("success") && result["success"] is bool b && b;
                if (!success)
                {
                    string errMsg = result.ContainsKey("error") ? result["error"]?.ToString()
                        : result.ContainsKey("title") ? result["title"]?.ToString()
                        : "Erreur d'enrôlement.";
                    lblRegisterError.Text = errMsg;
                    return;
                }

                SessionHelper.IsEnrolled = true;
                Response.Redirect("Users.aspx");
            }
            catch (Exception ex)
            {
                lblRegisterError.Text = "Erreur : " + ex.Message;
            }
        }

        protected void btnContinue_Click(object sender, EventArgs e)
        {
            if (!SessionHelper.IsEnrolled) { lblRegisterError.Text = "Enrôlement WebAuthn obligatoire avant de continuer."; return; }
            Response.Redirect("Users.aspx");
        }

        protected void btnSkip_Click(object sender, EventArgs e)
        {
            lblRegisterError.Text = "L'enrôlement WebAuthn est obligatoire.";
        }
    }
}
