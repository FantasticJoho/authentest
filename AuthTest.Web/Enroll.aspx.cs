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

        protected async void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                if (!SessionHelper.IsAuthenticated)
                {
                    Response.Redirect("Login.aspx");
                    return;
                }

                // Load registration options
                try
                {
                    var options = await ApiClient.PostAsync<object>("webauthn/register/begin", new { });
                    hdnCreationOptions.Value = Json.Serialize(options);
                }
                catch (Exception ex)
                {
                    lblRegisterError.Text = "Erreur lors du chargement des options d'enregistrement: " + ex.Message;
                }
            }
        }

        protected async void btnRegisterComplete_Click(object sender, EventArgs e)
        {
            await RegisterCompleteAsync();
        }

        private async Task RegisterCompleteAsync()
        {
            var attestationJson = hdnAttestationResponse.Value;
            if (string.IsNullOrEmpty(attestationJson))
            {
                lblRegisterError.Text = "Réponse d'attestation manquante.";
                return;
            }

            try
            {
                var attestationResponse = Json.Deserialize<object>(attestationJson);
                var result = await ApiClient.PostAsync<Dictionary<string, object>>("webauthn/register/complete", new { attestationResponse });

                bool success = result.ContainsKey("success") && (bool)result["success"];
                if (!success)
                {
                    lblRegisterError.Text = result.ContainsKey("error") ? result["error"]?.ToString() : "Erreur d'enregistrement WebAuthn.";
                    return;
                }

                lblRegisterError.Text = "Clé enregistrée avec succès !";
                lblRegisterError.CssClass = "success";
                SessionHelper.IsEnrolled = true;
            }
            catch (Exception ex)
            {
                lblRegisterError.Text = "Erreur: " + ex.Message;
            }
        }

        protected void btnContinue_Click(object sender, EventArgs e)
        {
            SessionHelper.IsEnrolled = true;
            Response.Redirect("Users.aspx");
        }

        protected void btnSkip_Click(object sender, EventArgs e)
        {
            SessionHelper.IsEnrolled = true;
            Response.Redirect("Users.aspx");
        }
    }
}
