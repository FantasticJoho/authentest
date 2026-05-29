<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Users.aspx.cs" Inherits="AuthTest.Web.UsersPage" Async="true" %>
<!DOCTYPE html>
<html>
<head runat="server">
  <title>Utilisateurs</title>
  <style>
    body { font-family: sans-serif; max-width: 800px; margin: 60px auto; }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
    .header h2 { margin: 0; }
    .logout-btn { padding: 8px 16px; background: #d13438; color: white; border: none; cursor: pointer; border-radius: 3px; }
    .logout-btn:hover { background: #a4373a; }
    table { width: 100%; border-collapse: collapse; margin-top: 20px; }
    table, th, td { border: 1px solid #ddd; }
    th { background-color: #f5f5f5; padding: 12px; text-align: left; }
    td { padding: 10px; }
    tr:hover { background-color: #f9f9f9; }
    .error { color: red; }
    .info { color: #0078d4; }
  </style>
</head>
<body>
  <form id="form1" runat="server">
    <div class="header">
      <h2>Utilisateurs enregistrés</h2>
      <asp:Button ID="btnLogout" runat="server" CssClass="logout-btn" Text="Déconnexion" OnClick="btnLogout_Click" />
    </div>

    <asp:Label ID="lblCurrentUser" runat="server" CssClass="info" />

    <asp:GridView ID="gvUsers" runat="server" AutoGenerateColumns="false">
      <Columns>
        <asp:BoundField DataField="Id" HeaderText="ID" />
        <asp:BoundField DataField="Username" HeaderText="Nom d'utilisateur" />
        <asp:BoundField DataField="Email" HeaderText="Email" />
        <asp:BoundField DataField="CreatedAt" HeaderText="Créé le" DataFormatString="{0:yyyy-MM-dd HH:mm:ss}" />
        <asp:BoundField DataField="HasWebAuthn" HeaderText="WebAuthn" />
      </Columns>
    </asp:GridView>

    <asp:Label ID="lblError" runat="server" CssClass="error" />
    <asp:Label ID="lblMessage" runat="server" />
  </form>
</body>
</html>
