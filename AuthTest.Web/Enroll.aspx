<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Enroll.aspx.cs" Inherits="AuthTest.Web.EnrollPage" Async="true" %>
<!DOCTYPE html>
<html>
<head runat="server">
  <meta charset="utf-8" />
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
          // Vue d'ensemble :
          // 1) lire les options préparées par le serveur,
          // 2) convertir les champs texte en binaire pour WebAuthn,
          // 3) demander au navigateur de créer une nouvelle clé,
          // 4) renvoyer le résultat au serveur pour enregistrement.

          // 1) Le serveur a déjà préparé des "creation options" WebAuthn
          //    et les a placées dans un champ caché HTML.
          //    Ce JSON contient notamment :
          //    - le challenge à signer,
          //    - l'utilisateur,
          //    - le relying party (RP),
          //    - les éventuelles clés déjà existantes à exclure.
          var optionsJson = document.getElementById('<%= hdnCreationOptions.ClientID %>').value;
          console.log("Options d'enregistrement (JSON)", optionsJson);
          // Si ce champ est vide, on ne peut pas démarrer l'enrôlement.
          // Le navigateur a besoin de ces options pour appeler WebAuthn.
          if (!optionsJson) {
            alert("Options d'enregistrement non disponibles");
            return;
          }

          // 2) On transforme le JSON texte en objet JavaScript.
          var options = JSON.parse(optionsJson);

          // 3) WebAuthn est très strict sur le RP ID.
          //    Le RP ID doit correspondre au host visible dans le navigateur
          //    (par exemple "localhost" ou "test.joho").
          //    Sinon le navigateur bloque avec un SecurityError.
          var browserHost = window.location.hostname;
          if (!options.rp) {
            // Cas défensif : si le serveur n'a pas fourni de bloc rp,
            // on le crée à partir du host courant.
            options.rp = { id: browserHost, name: 'AuthTest' };
          } else {
            // Ici on force le rp.id à la valeur du host courant.
            // C'est utile en dev pour éviter un décalage entre :
            // - l'URL ouverte dans le navigateur
            // - la valeur construite côté serveur
            options.rp.id = browserHost;
          }

          // 4) Les helpers ci-dessous convertissent entre :
          //    - Base64URL (format texte utilisé dans JSON / Web API)
          //    - ArrayBuffer (format binaire attendu par WebAuthn côté navigateur)
          //
          //    Beaucoup de champs WebAuthn voyagent en JSON côté serveur,
          //    mais navigator.credentials.create(...) attend du binaire.
          function b64urlToBuffer(b64url) {
            var b64 = b64url.replace(/-/g, '+').replace(/_/g, '/');
            var bin = atob(b64);
            var buf = new Uint8Array(bin.length);
            for (var i = 0; i < bin.length; i++) buf[i] = bin.charCodeAt(i);
            return buf.buffer;
          }

          // Conversion inverse : on reprend du binaire renvoyé par WebAuthn
          // pour le sérialiser en JSON et l'envoyer au serveur.
          function bufToB64url(buf) {
            var bin = String.fromCharCode.apply(null, new Uint8Array(buf));
            return btoa(bin).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
          }

          // 5) On convertit les champs qui DOIVENT être binaires avant l'appel WebAuthn.
          //    - challenge : défi anti-rejeu généré par le serveur
          //    - user.id : identifiant technique de l'utilisateur
          options.challenge = b64urlToBuffer(options.challenge);
          options.user.id = b64urlToBuffer(options.user.id);

          // excludeCredentials sert à dire au navigateur :
          // "n'utilise pas une clé déjà enregistrée pour ce compte".
          // Cela évite des doublons lors d'un nouvel enrôlement.
          if (options.excludeCredentials) {
            options.excludeCredentials = options.excludeCredentials.map(function(c) {
              return { id: b64urlToBuffer(c.id), type: c.type };
            });
          }

          // 6) Appel principal WebAuthn.
          //    Le navigateur / système d'exploitation va demander à la plateforme
          //    de créer une nouvelle credential (passkey / clé de sécurité).
          //    Si l'utilisateur annule, cette ligne lèvera une exception.
          var attestation = await navigator.credentials.create({ publicKey: options });

          // 7) On récupère quelques métadonnées utiles après création.
          //    - transports : USB, NFC, internal, etc.
          //    - clientExts : extensions WebAuthn éventuellement retournées
          var transports = attestation.response.getTransports ? attestation.response.getTransports() : [];
          var clientExts = attestation.getClientExtensionResults ? attestation.getClientExtensionResults() : {};

          // 8) Le navigateur renvoie un objet complexe avec des buffers binaires.
          //    On le transforme en JSON sérialisable pour pouvoir le poster au serveur.
          //    Le serveur validera ensuite l'attestation et enregistrera la clé.
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

          // 9) On stocke la réponse WebAuthn dans un champ caché,
          //    puis on déclenche le bouton serveur WebForms pour terminer
          //    la validation côté backend.
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
