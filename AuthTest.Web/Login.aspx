<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="AuthTest.Web.LoginPage" Async="true" %>
<!DOCTYPE html>
<html>
<head runat="server">
  <title>Connexion</title>
  <style>
    body { font-family: sans-serif; max-width: 400px; margin: 60px auto; }
    .field { margin-bottom: 12px; }
    label { display: block; margin-bottom: 4px; }
    input[type=text], input[type=password] { width: 100%; padding: 8px; box-sizing: border-box; }
    button { padding: 10px 20px; background: #0078d4; color: white; border: none; cursor: pointer; }
    .error { color: red; }
  </style>
</head>
<body>
  <form id="form1" runat="server">
    <h2>Connexion</h2>
    <asp:Label ID="lblError" runat="server" CssClass="error" />

    <!-- Step 1: username -->
    <asp:Panel ID="pnlUsername" runat="server">
      <div class="field">
        <label>Nom d'utilisateur</label>
        <asp:TextBox ID="txtUsername" runat="server" />
      </div>
      <asp:Button ID="btnCheckUsername" runat="server" Text="Continuer" OnClick="btnCheckUsername_Click" />
    </asp:Panel>

    <!-- Step 2a: password (hasWebAuthn=false) -->
    <asp:Panel ID="pnlPassword" runat="server" Visible="false">
      <div class="field">
        <label>Mot de passe</label>
        <asp:TextBox ID="txtPassword" runat="server" TextMode="Password" />
      </div>
      <asp:Button ID="btnLogin" runat="server" Text="Se connecter" OnClick="btnLogin_Click" />
    </asp:Panel>

    <!-- Step 2b: WebAuthn (hasWebAuthn=true) -->
    <asp:Panel ID="pnlWebAuthn" runat="server" Visible="false">
      <p>Authentification WebAuthn disponible.</p>
      <asp:HiddenField ID="hdnAssertionOptions" runat="server" />
      <asp:HiddenField ID="hdnAssertionResponse" runat="server" />
      <button type="button" onclick="startWebAuthn()">Authentifier avec la clé</button>
      <asp:Button ID="btnWebAuthnComplete" runat="server" Text="Valider" OnClick="btnWebAuthnComplete_Click" style="display:none" />
      <script>
        async function startWebAuthn() {
          var optionsJson = document.getElementById('<%= hdnAssertionOptions.ClientID %>').value;
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
          if (options.allowCredentials) {
            options.allowCredentials = options.allowCredentials.map(function(c) {
              return { id: b64urlToBuffer(c.id), type: c.type };
            });
          }

          var assertion = await navigator.credentials.get({ publicKey: options });
          var resp = {
            id: bufToB64url(assertion.rawId),
            rawId: bufToB64url(assertion.rawId),
            type: assertion.type,
            response: {
              authenticatorData: bufToB64url(assertion.response.authenticatorData),
              clientDataJSON: bufToB64url(assertion.response.clientDataJSON),
              signature: bufToB64url(assertion.response.signature),
              userHandle: assertion.response.userHandle ? bufToB64url(assertion.response.userHandle) : null
            }
          };
          document.getElementById('<%= hdnAssertionResponse.ClientID %>').value = JSON.stringify(resp);
          document.getElementById('<%= btnWebAuthnComplete.ClientID %>').click();
        }
      </script>
    </asp:Panel>
  </form>
</body>
</html>
