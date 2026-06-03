<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Users.aspx.cs" Inherits="AuthTest.Web.UsersPage" Async="true" %>
<!DOCTYPE html>
<html>
<head runat="server">
  <meta charset="utf-8" />
  <title>Utilisateurs</title>
  <style>
    body { font-family: sans-serif; max-width: 900px; margin: 40px auto; }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
    .header h2 { margin: 0; }
    table { width: 100%; border-collapse: collapse; margin-top: 10px; }
    th, td { border: 1px solid #ddd; padding: 10px; text-align: left; }
    th { background: #f5f5f5; }
    .error { color: red; }
    .info { color: #0078d4; }
    .success { color: green; }
    .btn-danger { padding: 5px 10px; background: #d13438; color: white; border: none; cursor: pointer; margin-right: 4px; }
    .btn-warning { padding: 5px 10px; background: #ca5010; color: white; border: none; cursor: pointer; }
  </style>
</head>
<body>
  <form id="form1" runat="server">
    <div class="header">
      <h2>Gestion des utilisateurs</h2>
      <asp:Button ID="btnLogout" runat="server" Text="Déconnexion" OnClick="btnLogout_Click" CssClass="btn-danger" />
    </div>
    <asp:Label ID="lblCurrentUser" runat="server" CssClass="info" /><br />
    <asp:Label ID="lblMessage" runat="server" />
    <asp:Label ID="lblError" runat="server" CssClass="error" />

    <asp:GridView ID="gvUsers" runat="server" AutoGenerateColumns="false" DataKeyNames="Id"
                  OnRowCommand="gvUsers_RowCommand">
      <Columns>
        <asp:BoundField DataField="Username" HeaderText="Utilisateur" />
        <asp:BoundField DataField="HasWebAuthn" HeaderText="2FA actif" />
        <asp:BoundField DataField="MustChangePassword" HeaderText="MDP à changer" />
        <asp:TemplateField HeaderText="Actions">
          <ItemTemplate>
            <asp:Button runat="server" Text="Reset 2FA" CommandName="Reset2FA"
                        CommandArgument='<%# Eval("Id") %>' CssClass="btn-warning" />
            <asp:Button runat="server" Text="Supprimer" CommandName="DeleteUser"
                        CommandArgument='<%# Eval("Id") %>' CssClass="btn-danger" />
          </ItemTemplate>
        </asp:TemplateField>
      </Columns>
    </asp:GridView>
  </form>
</body>
</html>
