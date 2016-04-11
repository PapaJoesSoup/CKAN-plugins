using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using mshtml;
using CKAN;

namespace SpaceDockPlugin
{

  public class SpaceDockPlugin : CKAN.IGUIPlugin
  {

    private readonly CKAN.Version _ckanVersion = new CKAN.Version("v1.0.0");
    private KSPVersion _kspVersion = Main.Instance.CurrentInstance.Version();
    private Dictionary<int, CkanModule> _spaceDockToCkanMap = new Dictionary<int, CkanModule>();

    public override void Initialize()
    {
      var registry = Main.Instance.CurrentInstance.Registry;
      _kspVersion = Main.Instance.CurrentInstance.Version();

      foreach (var module in registry.Available(_kspVersion))
      {
        var latest = registry.LatestAvailable(module.identifier, _kspVersion);
        if (latest.resources != null)
        {
          if (latest.resources.spacedock != null)
          {
            var ksId = int.Parse(latest.resources.spacedock.ToString().Split('/')[2]);
            _spaceDockToCkanMap[ksId] = latest;
          }
        }
      }

      var webBrowser = new WebBrowser
      {
        Dock = DockStyle.Fill,
        Url = new Uri("http://spacedock.info", UriKind.Absolute)
      };

      webBrowser.DocumentCompleted += (sender, args) =>
      {
        var thumbnails = GetElementsByClass(webBrowser.Document, "thumbnail");
        foreach (var thumbnail in thumbnails)
        {
          var url = "";
          HtmlElement activeElement = null;
          // Find url
          foreach (HtmlElement child in thumbnail.Children)
          {
            if (child.InnerHtml.Contains("href"))
            {
              url = child.GetAttribute("href");
              break;
            }
            if (child.InnerHtml.Contains("ksp-update") && activeElement == null) activeElement = child;
          }
          var sdModId = int.Parse(url.Split('/')[2]);

          var module = CkanModuleForSpaceDockId(sdModId);
          if (module != null && activeElement != null)
          {
            activeElement.InnerHtml = "<span class='badge' title='This mod is listed in CKAN.'>CKAN</span>" + activeElement.InnerHtml;
            if (IsModuleInstalled(module.identifier))
              activeElement.InnerHtml += "<div style=\"margin-top: 32px;\" class=\"ksp-update\">Installed</div>";
          }
        }

        HtmlElement downloadLink = webBrowser.Document.GetElementById("download-link-primary");
        if (downloadLink == null) return;
        int modId = -1;

        var downloadUrl = downloadLink.GetAttribute("href");
        if (downloadUrl.StartsWith("#"))
        {
          modId = int.Parse(downloadUrl.Substring(1));
        }
        else if (!int.TryParse(downloadUrl.Split('/')[2], out modId))
        {
          modId = -1;
        }

        var ckanModule = CkanModuleForSpaceDockId(modId);
        if (ckanModule != null)
        {
          downloadLink.SetAttribute("href", "#" + modId.ToString());

          if (IsModuleInstalled(ckanModule.identifier))
          {
            downloadLink.InnerHtml = "Installed";
          }
          else if (IsModuleSelectedForInstall(ckanModule.identifier))
          {
            downloadLink.InnerHtml = "Selected for install";
          }
          else
          {
            downloadLink.InnerHtml = "Add to CKAN install";

            webBrowser.Document.Body.MouseDown += (o, e) =>
            {
              switch (e.MouseButtonsPressed)
              {
                case MouseButtons.Left:
                  HtmlElement element = webBrowser.Document.GetElementFromPoint(e.ClientMousePosition);
                  if (element != null && element.Id == "download-link-primary")
                  {
                    SelectModuleForInstall(ckanModule.identifier);
                  }
                  break;
              }
            };
          }
        }
      };

      var tabPage = new TabPage
      {
        Name = "SpaceDockBrowserBrowserTabPage",
        Text = "SpaceDock"
      };

      tabPage.Controls.Add(webBrowser);

      Main.Instance.m_TabController.m_TabPages.Add("SpaceDockBrowser", tabPage);
      Main.Instance.m_TabController.ShowTab("SpaceDockBrowser", 1, false);
    }

    static IEnumerable<HtmlElement> GetElementsByClass(HtmlDocument doc, string className)
    {
      return from HtmlElement e in doc.All where e.GetAttribute("className") == className select e;
    }

    public override void Deinitialize()
    {
      Main.Instance.m_TabController.HideTab("SpaceDockBrowser");
      Main.Instance.m_TabController.m_TabPages.Remove("SpaceDockBrowser");
    }

    public override string GetName()
    {
      return "SpaceDockPlugin by Papa_Joe";
    }

    public override CKAN.Version GetVersion()
    {
      return _ckanVersion;
    }

    private bool IsModuleInstalled(string identifier)
    {
      var registry = Main.Instance.CurrentInstance.Registry;
      return registry.IsInstalled(identifier);
    }

    private bool IsModuleSelectedForInstall(string identifier)
    {
      if (IsModuleInstalled(identifier)) return false;
      IUser user = Main.Instance.CurrentInstance.User;
      var registry = Main.Instance.CurrentInstance.Registry;
      var installer = ModuleInstaller.GetInstance(Main.Instance.CurrentInstance, user);
      var changes = Main.Instance.mainModList.ComputeUserChangeSet();
      var changeset = Main.Instance.mainModList.ComputeChangeSetFromModList(registry, changes, installer, _kspVersion).Result;
      
      foreach (var change in changeset)
      {
        if (change.ChangeType == GUIModChangeType.Install)
        {
          if (change.Mod.Identifier == identifier)
          {
            return true;
          }
        }
      }
      return false;
    }

    private void SelectModuleForInstall(string identifier)
    {
      foreach (DataGridViewRow row in Main.Instance.ModList.Rows)
      {
        var mod = ((GUIMod)row.Tag);
        if (mod.Identifier == identifier)
        {
          (row.Cells[0] as DataGridViewCheckBoxCell).Value = true;
          mod.IsInstallChecked = true;
        }
      }
    }

    private CkanModule CkanModuleForSpaceDockId(int id)
    {
      if (!_spaceDockToCkanMap.ContainsKey(id))
      {
        return null;
      }
      return _spaceDockToCkanMap[id];
    }
  }
}
