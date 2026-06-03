<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="AuthTest.Web.LoginPage" Async="true" %>
<!DOCTYPE html>
<html>
<head runat="server">
  <meta charset="utf-8" />
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
          // Vue d'ensemble :
          // 1) lire les options préparées par le serveur,
          // 2) convertir les champs texte en binaire pour WebAuthn,
          // 3) demander au navigateur de prouver qu'il possède une clé valide,
          // 4) renvoyer la preuve au serveur pour vérification.

          // 1) Le serveur a déjà préparé des "assertion options" WebAuthn
          //    et les a stockées dans un champ caché.
          //    Ce JSON contient notamment :
          //    - le challenge à signer,
          //    - le RP ID attendu,
          //    - la liste éventuelle des credentials autorisées.
          var optionsJson = document.getElementById('<%= hdnAssertionOptions.ClientID %>').value;

          // On transforme le JSON texte en objet JavaScript utilisable.
          var options = JSON.parse(optionsJson);

          // 2) En authentification, rpId doit correspondre au host courant.
          //    Exemple :
          //    - si l'utilisateur est sur https://test.joho:8081, rpId doit être "test.joho"
          //    - si l'utilisateur est sur https://localhost:8081, rpId doit être "localhost"
          //    Sinon le navigateur peut refuser l'opération.
          options.rpId = window.location.hostname;

          // 3) Comme pour l'enrôlement, WebAuthn manipule des données binaires.
          //    Le JSON venant du serveur encode ces valeurs en Base64URL,
          //    donc on doit les reconvertir en ArrayBuffer avant l'appel navigateur.
          function b64urlToBuffer(b64url) {
            var b64 = b64url.replace(/-/g, '+').replace(/_/g, '/');
            var bin = atob(b64);
            var buf = new Uint8Array(bin.length);
            for (var i = 0; i < bin.length; i++) buf[i] = bin.charCodeAt(i);
            return buf.buffer;
          }

          // Conversion inverse pour remettre la réponse WebAuthn en JSON
          // avant de la renvoyer au serveur.
          function bufToB64url(buf) {
            var bin = String.fromCharCode.apply(null, new Uint8Array(buf));
            return btoa(bin).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
          }

          // 4) Le challenge est la donnée principale à signer.
          //    Il est généré par le serveur pour prouver que la réponse
          //    correspond bien à cette tentative d'authentification.
          options.challenge = b64urlToBuffer(options.challenge);

          // allowCredentials limite l'authentification à certaines clés connues.
          // Si cette liste est fournie, chaque id doit aussi être converti en binaire.
          if (options.allowCredentials) {
            options.allowCredentials = options.allowCredentials.map(function(c) {
              return { id: b64urlToBuffer(c.id), type: c.type };
            });
          }

          // 5) Appel principal d'authentification WebAuthn.
          //    Le navigateur demande à la plateforme de prouver qu'une clé valide
          //    pour ce RP et cet utilisateur est disponible.
          //    L'utilisateur peut avoir à confirmer avec PIN, biométrie, etc.
          var assertion = await navigator.credentials.get({ publicKey: options });

          // 6) La réponse contient plusieurs buffers binaires signés par l'authenticator.
          //    On les convertit en Base64URL pour construire un JSON sérialisable.
          //    Ce JSON sera ensuite validé côté serveur.
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

          // 7) On place la réponse dans un champ caché puis on déclenche
          //    le bouton serveur WebForms pour terminer la vérification côté backend.
          document.getElementById('<%= hdnAssertionResponse.ClientID %>').value = JSON.stringify(resp);
          document.getElementById('<%= btnWebAuthnComplete.ClientID %>').click();
        }
      </script>
    </asp:Panel>
  </form>
</body>
</html>
