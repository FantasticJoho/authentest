<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="ChangePassword.aspx.cs" Inherits="AuthTest.Web.ChangePasswordPage" Async="true" %>
<!DOCTYPE html>
<html>
<head runat="server">
  <title>Changer le mot de passe</title>
  <style>
    body { font-family: sans-serif; max-width: 400px; margin: 60px auto; }
    .field { margin-bottom: 12px; }
    label { display: block; margin-bottom: 4px; }
    input[type=text], input[type=password] { width: 100%; padding: 8px; box-sizing: border-box; }
    button { padding: 10px 20px; background: #0078d4; color: white; border: none; cursor: pointer; }
    .error { color: red; }
    .success { color: green; }
  </style>
</head>
<body>
  <form id="form1" runat="server">
    <h2>Changer le mot de passe</h2>
    <asp:Label ID="lblMessage" runat="server" CssClass="error" />

    <div class="field">
      <label>Nouveau mot de passe</label>
      <asp:TextBox ID="txtNewPassword" runat="server" TextMode="Password" />
    </div>

    <div class="field">
      <label>Confirmer le mot de passe</label>
      <asp:TextBox ID="txtConfirmPassword" runat="server" TextMode="Password" />
    </div>

    <asp:Button ID="btnChangePassword" runat="server" Text="Changer le mot de passe" OnClick="btnChangePassword_Click" />
  </form>
</body>
</html>
