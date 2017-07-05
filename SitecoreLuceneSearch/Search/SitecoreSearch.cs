using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Sitecore.Data.Items;
using Sitecore.Search;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.Data;
using Lucene.Net.Documents;
using System.Windows;
using System.Net;
using System.IO;
using Lucene.Net.Store;
using Lucene.Net.Index;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
//using Sitecore.Web.UI.WebControls;


//using System.Windows.Forms;
using System.Text;
using System.Text.RegularExpressions;
using SitecoreFields = Sitecore.Data.Fields;
using HtmlAgilityPack;

namespace SitecoreLuceneSearch.Search
{
    public class SitecoreSearch
    {

        private static readonly Regex _tags_ = new Regex(@"<[^>]+?>", RegexOptions.Multiline | RegexOptions.Compiled);

        //add characters that are should not be removed to this regex
        private static readonly Regex _notOkCharacter_ = new Regex(@"[^\w;&#@.:/\\?=|%!() -]", RegexOptions.Compiled);

        string IndexFolderPath = @"C:\inetpub\wwwroot\Bupa.Com\Data\indexes"; //pending, need to do it

        public struct LargestImage
        {
            public string imgSrc;
            public double imgDimension;
            public string imgAlt;
        }

        private static string _luceneDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "externalURLs_lucene_index");
        private static FSDirectory _directoryTemp;
        private static FSDirectory _directory
        {
            // returns the directory where indexes will be stored
            get
            {
                if (_directoryTemp == null)
                {
                    _directoryTemp = FSDirectory.Open(new DirectoryInfo(_luceneDir));
                }
                if (IndexWriter.IsLocked(_directoryTemp))
                {
                    IndexWriter.Unlock(_directoryTemp);
                }
                var lockFilePath = Path.Combine(_luceneDir, "write.lock");

                if (File.Exists(lockFilePath))
                {
                    File.Delete(lockFilePath);
                }
                return _directoryTemp;
            }
        }

        public List<Item> indexSitecoreContent()
        {



            // ID blogTemplateId = new Sitecore.Data.ID("{48587868-4A19-48BB-8FC9-F1DD31CB6C8E}");
            //var index = Sitecore.ContentSearch.ContentSearchManager.GetIndex("sitecore_master_index");
            //List<Item> results = new List<Item>();

            //using (Sitecore.ContentSearch.IProviderSearchContext context = index.CreateSearchContext())
            //{
            //    //var searchResults = context.GetQueryable<SearchResultItem>().Where(x => x.Content.Contains("Sitecore")); 
            //    var searchResults = context.GetQueryable<SearchResultItem>().Take(10);
            //    results = (List<Item>)searchResults;
            //}



            #region Indexing

            // Get the indexes files from the "lucene_Index" folder
            string[] filePaths = System.IO.Directory.GetFiles(_luceneDir);

            // Delete all the indexes from "lucene_Index" folder
            foreach (string filePath in filePaths)
            {
                File.Delete(filePath);
            }


            //Create Directory for Indexes
            //There are 2 options, FS or RAM
            //Step 1: Declare Index Store


            //Now we need Analyzer
            //An Analyzer builds TokenStreams, which analyze text. It thus represents a policy for extracting index terms from text.
            //In general, any analyzer in Lucene is tokenizer + stemmer + stop-words filter.   
            //Tokenizer splits your text into chunks-For example, for phrase "I am very happy" it will produce list ["i", "am", "very", "happy"] 
            // stemmer:-piece of code responsible for “normalizing” words to their common form (horses => horse, indexing => index, etc)
            //Stop words are the most frequent and almost useless words
            Analyzer analyzer = new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30);

            //Need an Index Writer to write the output of Analysis to Index
            IndexWriter writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED);

            // Get the data to index for search
            Item ExternalLinksFolder = Sitecore.Context.Database.GetItem("{6BD289C4-A964-4915-A769-A79C897D1746}");

            string htmlContent = string.Empty;

           // string[] ExternalLinksHtmlContent = new string[ExternalLinksFolder.Children.Count];
           // string ExternalPageHtmlContent;
            int count = 0;
            Sitecore.Data.Fields.ImageField imageField=null;
            SitecoreFields.LinkField linkfield =null;
            string externalurl;
            // web page declaring 
            var htmlWeb = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument document = new HtmlAgilityPack.HtmlDocument();
            string webPageTitle;
            string webPageContent;


            // end declaring
            foreach (Item externalLinkItem in ExternalLinksFolder.Children)
            {
                Document doc = new Document();
                linkfield = externalLinkItem.Fields["ExternalURL"];
                externalurl = linkfield.Url;

                imageField = externalLinkItem.Fields["Image"];

                // webpage Title field
                LargestImage largestImage = new LargestImage();
                HtmlAgilityPack.HtmlDocument htmlDocument = new HtmlAgilityPack.HtmlDocument();
                htmlDocument = htmlWeb.Load(externalurl);
                webPageTitle = GetWebPageTitle(htmlDocument);
                webPageContent = GetPageContent(htmlDocument);
                largestImage = GetLargestWebPageSec(htmlDocument, externalurl);
                // image field
                if (largestImage.imgSrc == "") // populate image attributes from sitecore image field if there is no image found on the external web page
                {
                    largestImage.imgSrc = Sitecore.Resources.Media.MediaManager.GetMediaUrl(imageField.MediaItem);
                    largestImage.imgAlt = imageField.Alt;
                }

                if(webPageTitle == "") // populate title from sitecore if webpage has no title or h1 tags
                {
                    webPageTitle = externalLinkItem.Fields["Title"].Value;
                }

                if (webPageContent == "")
                {
                    webPageContent = externalLinkItem.Fields["PageContent"].Value;
                }

                // end image field
                doc.Add(new Field("External_WebPage_Content", webPageContent, Field.Store.YES, Field.Index.ANALYZED));
                doc.Add(new Field("External_WebPage_Title", webPageTitle, Field.Store.YES, Field.Index.ANALYZED));
                doc.Add(new Field("External_WebPage_Url", externalurl, Field.Store.YES, Field.Index.NOT_ANALYZED));
                doc.Add(new Field("External_WebPage_ImageSrc", largestImage.imgSrc, Field.Store.YES, Field.Index.NOT_ANALYZED));
                doc.Add(new Field("External_WebPage_ImageAlt", largestImage.imgAlt, Field.Store.YES, Field.Index.NOT_ANALYZED));
                
                writer.AddDocument(doc);
                count++;
            }

            writer.Optimize();
            writer.Commit();
            writer.Dispose();
            #endregion

            return results;
        }

        private static string GetPageContent(HtmlDocument doc)
        {
            doc.DocumentNode.Descendants().Where(n => n.Name == "script" || n.Name == "style" || n.Name == "noscript").ToList().ForEach(n => n.Remove());
            doc.DocumentNode.SelectNodes("//comment()").ToList().ForEach(n => n.Remove());
            string pageContent = UnHtml(doc.DocumentNode.SelectSingleNode("//body").InnerText.ToString());
            return pageContent;
        }

        public static String UnHtml(String html)
        {
            html = HttpUtility.UrlDecode(html);
            html = HttpUtility.HtmlDecode(html);

            //replace matches of these regexes with space
            html = _tags_.Replace(html, " ");
            html = _notOkCharacter_.Replace(html, " ");
            html = SingleSpacedTrim(html);

            return html;
        }

        private static String SingleSpacedTrim(String inString)
        {
            StringBuilder sb = new StringBuilder();
            Boolean inBlanks = false;
            foreach (Char c in inString)
            {
                switch (c)
                {
                    case '\r':
                    case '\n':
                    case '\t':
                    case ' ':
                        if (!inBlanks)
                        {
                            inBlanks = true;
                            sb.Append(' ');
                        }
                        continue;
                    default:
                        inBlanks = false;
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString().Trim();
        }

        public List<Item> results { get; set; }

        private static LargestImage GetLargestWebPageSec(HtmlDocument doc, string webPageUrl)
        {
            // for getting domain from the url
            var url = new Uri(webPageUrl);
            int protocalAndHostnameLength = url.AbsoluteUri.Count() - url.AbsolutePath.Count();
            string protocalAndHostname = url.ToString().Substring(0, protocalAndHostnameLength);
            // end domain
            //  request for images
            WebClient webClient = new WebClient();
            byte[] imageBytes = null;
            MemoryStream memoryStream = null;
            System.Drawing.Image image;
            LargestImage largestImage = new LargestImage();
            string imageURL = "";
            // end request
            int count = 0;


            // Now, using LINQ to get all Images from the webpage
            List<HtmlNode> imageNodes = null;
            if (doc.DocumentNode.SelectNodes("//img") != null)
            {
                imageNodes = (from HtmlNode node in doc.DocumentNode.SelectNodes("//img")
                              where node.Name == "img"
                              select node).ToList();


                foreach (HtmlNode node in imageNodes)
                {
                    if (!string.IsNullOrEmpty(node.Attributes["src"].Value) && !node.Attributes["src"].Value.EndsWith(".gif"))
                    {
                        if (node.Attributes["src"].Value.StartsWith("h"))
                        {
                            imageURL = node.Attributes["src"].Value;
                        }
                        else
                        {
                            imageURL = (node.Attributes["src"].Value.StartsWith("/")) ? protocalAndHostname + node.Attributes["src"].Value : protocalAndHostname + "/" + node.Attributes["src"].Value;
                        }

                        imageBytes = webClient.DownloadData(imageURL);

                        if (imageBytes.Count() > 0)
                        {
                            memoryStream = new MemoryStream(imageBytes);
                            image = System.Drawing.Image.FromStream(memoryStream);

                            if (image.Height > 0 && image.Width > 0)
                            {
                                if (count == 0)
                                {
                                    largestImage.imgSrc = imageURL;
                                    largestImage.imgDimension = image.Width * image.Height;
                                    if (node.Attributes["alt"] != null)
                                    {
                                        largestImage.imgAlt = node.Attributes["alt"].Value;
                                    }
                                }

                                if ((image.Width * image.Height) > largestImage.imgDimension)
                                {
                                    largestImage.imgSrc = imageURL;
                                    largestImage.imgDimension = image.Width * image.Height;
                                    if (node.Attributes["alt"] != null)
                                    {
                                        largestImage.imgAlt = node.Attributes["alt"].Value;
                                    }
                                }
                            }
                        }

                        count++;

                    }

                }
            }


            return largestImage;
        }
     
        private static string GetWebPageTitle(HtmlDocument doc)
        {
            // get the title of the webpage
            string pageTitle = "";

            if (doc.DocumentNode.SelectSingleNode("//title") != null)
            {
                pageTitle = doc.DocumentNode.SelectSingleNode("//title").InnerText.Trim();
            }
            // if title is not present
            if (pageTitle == "" && doc.DocumentNode.SelectNodes("//h1") != null)
            {
                pageTitle = doc.DocumentNode.SelectNodes("//h1").FirstOrDefault().InnerHtml.Trim();
            }
            return pageTitle;
        }


    }
}
