using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Layouts;
using Sitecore.Pipelines.HttpRequest;
using Sitecore.SecurityModel;
using Sitecore.Sites;
using Sitecore.Text;
using Sitecore.Web;
using System;
using System.Collections.Generic;
using System.Web;

namespace Sitecore.Support.Pipelines.HttpRequest
{
  public class ExecuteRequest : Sitecore.Pipelines.HttpRequest.ExecuteRequest
  {
    public override void Process(HttpRequestArgs args)
    {
      Assert.ArgumentNotNull(args, "args");
      SiteContext site = Context.Site;
      if (site != null && !SiteManager.CanEnter(site.Name, Context.User))
      {
        HandleSiteAccessDenied(site, args);
      }
      else
      {
        if (Context.PageMode.IsPreview && args.PermissionDenied)
        {
          HandleItemNotFound(args);
        }
        PageContext page = Context.Page;
        Assert.IsNotNull(page, "No page context in processor.");
        string filePath = page.FilePath;
        if (filePath.Length > 0)
        {
          if (WebUtil.IsExternalUrl(filePath))
          {
            args.Context.Response.Redirect(filePath, true);
          }
          else if (string.Compare(filePath, HttpContext.Current.Request.Url.LocalPath,
                     StringComparison.InvariantCultureIgnoreCase) != 0)
          {
            args.Context.RewritePath(filePath, args.Context.Request.PathInfo, args.Url.QueryString, false);
          }
        }
        else if (Context.Item == null)
        {
          HandleItemNotFound(args);
        }
        else
        {
          HandleLayoutNotFound(args);
        }
      }
    }

    private string GetNoAccessUrl(out bool loginPage)
    {
      SiteContext site = Context.Site;
      loginPage = false;
      if (site != null && site.LoginPage.Length > 0)
      {
        if (SiteManager.CanEnter(site.Name, Context.User))
        {
          Tracer.Info("Redirecting to login page \"" + site.LoginPage + "\".");
          loginPage = true;
          return site.LoginPage;
        }
        Tracer.Info("Redirecting to the 'No Access' page as the current user '" + Context.User.Name + "' does not have sufficient rights to enter the '" + site.Name + "' site.");
        return Settings.NoAccessUrl;
      }
      Tracer.Warning("Redirecting to \"No Access\" page as no login page was found.");
      return Settings.NoAccessUrl;
    }

    private void HandleItemNotFound(HttpRequestArgs args)
    {
      string localPath = args.LocalPath;
      string name = Context.User.Name;
      bool flag = false;
      bool flag2 = false;
      string url = Settings.ItemNotFoundUrl;
      if (args.PermissionDenied)
      {
        flag = true;
        url = GetNoAccessUrl(out flag2);
      }
      SiteContext site = Context.Site;
      string text = (site != null) ? site.Name : string.Empty;
      List<string> list = new List<string>(new string[6]
      {
            "item",
            localPath,
            "user",
            name,
            "site",
            text
      });
      if (Settings.Authentication.SaveRawUrl)
      {
        list.AddRange(new string[2]
        {
                "url",
                HttpUtility.UrlEncode(Context.RawUrl)
        });
      }
      url = WebUtil.AddQueryString(url, list.ToArray());
      if (!flag)
      {
        Log.Warn($"Request is redirected to document not found page. Requested url: {Context.RawUrl}, User: {name}, Website: {text}", this);
        RedirectOnItemNotFound(url);
      }
      else
      {
        if (flag2)
        {
          Log.Warn($"Request is redirected to login page. Requested url: {Context.RawUrl}, User: {name}, Website: {text}", this);
          RedirectToLoginPage(url);
        }
        Log.Warn($"Request is redirected to access denied page. Requested url: {Context.RawUrl}, User: {name}, Website: {text}", this);
        RedirectOnNoAccess(url);
      }
    }

    private void HandleLayoutNotFound(HttpRequestArgs args)
    {
      string text = string.Empty;
      string text2 = string.Empty;
      string text3 = string.Empty;
      string text4 = "Request is redirected to no layout page.";
      DeviceItem device = Context.Device;
      if (device != null)
      {
        text2 = device.Name;
      }
      Item item = Context.Item;
      if (item != null)
      {
        text4 = text4 + " Item: " + item.Uri;
        if (device != null)
        {
          text4 += $" Device: {device.ID} ({device.InnerItem.Paths.Path})";
          text = item.Visualization.GetLayoutID(device).ToString();
          if (text.Length > 0)
          {
            Database database = Context.Database;
            Assert.IsNotNull(database, "No database on processor.");
            Item item2 = ItemManager.GetItem(text, Language.Current, Sitecore.Data.Version.Latest, database, SecurityCheck.Disable);
            if (item2 != null && !item2.Access.CanRead())
            {
              SiteContext site = Context.Site;
              string text5 = (site != null) ? site.Name : string.Empty;
              text3 = WebUtil.AddQueryString(Settings.NoAccessUrl, "item", "Layout: " + text + " (item: " + args.LocalPath + ")", "user", Context.GetUserName(), "site", text5, "device", text2);
            }
          }
        }
      }
      if (text3.Length == 0)
      {
        text3 = WebUtil.AddQueryString(Settings.LayoutNotFoundUrl, "item", args.LocalPath, "layout", text, "device", text2);
      }
      Log.Warn(text4, this);
      RedirectOnLayoutNotFound(text3);
    }

    private void HandleSiteAccessDenied(SiteContext site, HttpRequestArgs args)
    {
      string noAccessUrl = Settings.NoAccessUrl;
      noAccessUrl = WebUtil.AddQueryString(noAccessUrl, "item", args.LocalPath, "user", Context.GetUserName(), "site", site.Name, "right", "site:enter");
      Log.Warn($"Request is redirected to access denied page. Requested url: {Context.RawUrl}, User: {Context.GetUserName()}, Website: {site.Name}", this);
      RedirectOnSiteAccessDenied(noAccessUrl);
    }
  }
}