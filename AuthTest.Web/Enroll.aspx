<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Enroll.aspx.cs" Inherits="AuthTest.Web.EnrollPage" Async="true" %>
<!DOCTYPE html>
<html>
<head runat="server">
  <title>Inscription WebAuthn</title>
  <style>
    body { font-family: sans-serif; max-width: 600px; margin: 60px auto; }
    .section { margin: 20px 0; padding: 15px; border: 1px solid #ddd; }
    .field { margin-bottom: 12px; }
    label { display: block; margin-bottom: 4px; font-weight: bold; }
    input[type=text], input[type=password] { width: 100%; padding: 8px; box-sizing: border-box; }
    button { padding: 10px 20px; background: #0078d4; color: white; border: none; cursor: pointer; margin: 5px 0; }
    button:hover { background: #005a9e; }
    .error { color: red; }
    .success { color: green; }
    .info { color: #0078d4; }
  </style>
</head>
<body>
  <form id="form1" runat="server">
    <h2>Inscription WebAuthn</h2>
    <asp:Label ID="lblMessage" runat="server" />

    <!-- Step 1: Register WebAuthn -->
    <asp:Panel ID="pnlRegisterWebAuthn" runat="server" CssClass="section">
      <h3>Ajouter une clé WebAuthn</h3>
      <p>Enregistrez une clé de sécurité pour l'authentification rapide.</p>
      <div class="field">
        <label>Nom de la clé</label>
        <asp:TextBox ID="txtKeyName" runat="server" />
      </div>
      <asp:HiddenField ID="hdnCreationOptions" runat="server" />
      <asp:HiddenField ID="hdnAttestationResponse" runat="server" />
      <button type="button" onclick="startRegistration()">Enregistrer une clé</button>
      <asp:Button ID="btnRegisterComplete" runat="server" Text="Valider" OnClick="btnRegisterComplete_Click" style="display:none" />
      <asp:Label ID="lblRegisterError" runat="server" CssClass="error" />
      <script>
        async function startRegistration() {
          var optionsJson = document.getElementById('<%= hdnCreationOptions.ClientID %>').value;
          if (!optionsJson) {
            alert("Options d'enregistrement non disponibles");
            return;
          }
          var options = JSON.parse(optionsJson);

          function b64urlToBuffer(b64url) {
            var b64 = b64url.replace(/-/g, '+').replace(/_/g, '/');
            var bin = atob(b64);
            var buf = new Uint8Array(bin.length);
            for (var i = 0; i < bin.length; i++) buf[i] = bin.charCodeAt(i);
            return buf.buffer;
          }
          function bufToB64url(buf) {
            var bin = String.fromCharCode.apply(null, new Uint8Array(buf));
            return btoa(bin).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
          }

          options.challenge = b64urlToBuffer(options.challenge);
          options.user.id = b64urlToBuffer(options.user.id);
          if (options.excludeCredentials) {
            options.excludeCredentials = options.excludeCredentials.map(function(c) {
              return { id: b64urlToBuffer(c.id), type: c.type };
            });
          }

          var attestation = await navigator.credentials.create({ publicKey: options });
          var transports = attestation.response.getTransports ? attestation.response.getTransports() : [];
          var clientExts = attestation.getClientExtensionResults ? attestation.getClientExtensionResults() : {};
          var resp = {
            id: bufToB64url(attestation.rawId),
            rawId: bufToB64url(attestation.rawId),
            type: attestation.type,
            response: {
              attestationObject: bufToB64url(attestation.response.attestationObject),
              clientDataJSON: bufToB64url(attestation.response.clientDataJSON),
              transports: transports
            },
            clientExtensionResults: clientExts
          };
          document.getElementById('<%= hdnAttestationResponse.ClientID %>').value = JSON.stringify(resp);
          document.getElementById('<%= btnRegisterComplete.ClientID %>').click();
        }
      </script>
    </asp:Panel>

    <!-- Step 2: Skip or Complete -->
    <asp:Panel ID="pnlComplete" runat="server" CssClass="section">
      <asp:Button ID="btnContinue" runat="server" Text="Continuer vers les utilisateurs" OnClick="btnContinue_Click" />
      <asp:Button ID="btnSkip" runat="server" Text="Ignorer" OnClick="btnSkip_Click" Visible="false" />
    </asp:Panel>
  </form>
</body>
</html>
