using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Sitecore.Data.Items;
using SitecoreLuceneSearch.Search;
namespace SitecoreLuceneSearch.Search
{
    public class sitecoreSearchController : Controller
    {
        // GET: sitecoreSearch
        public ActionResult Index()
        {
            SitecoreSearch sitecoreSearch = new SitecoreSearch();
            List<Item> results = sitecoreSearch.indexSitecoreContent();
            return View("~/Views/sitecoreSearch/Index.cshtml");
        }
    }
}