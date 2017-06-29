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


using System.Windows.Forms;
using System.Text;
using System.Text.RegularExpressions;
using SitecoreFields = Sitecore.Data.Fields;
namespace SitecoreLuceneSearch.Search
{
    public class SitecoreSearch
    {

        private static readonly Regex _tags_ = new Regex(@"<[^>]+?>", RegexOptions.Multiline | RegexOptions.Compiled);

        //add characters that are should not be removed to this regex
        private static readonly Regex _notOkCharacter_ = new Regex(@"[^\w;&#@.:/\\?=|%!() -]", RegexOptions.Compiled);

        string IndexFolderPath = @"C:\inetpub\wwwroot\Bupa.Com\Data\indexes";

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

        // Image begin
        //struct ImageItem
        //{
        //    public string src;
        //    public string alt;
        //};
        // end image

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

            string[] ExternalLinksHtmlContent = new string[ExternalLinksFolder.Children.Count];

            int count = 0;

            Sitecore.Data.Fields.ImageField imageField=null;
             SitecoreFields.LinkField linkfield =null;
            foreach (Item externalLinkItem in ExternalLinksFolder.Children)
            {
                Document doc = new Document();
                linkfield = externalLinkItem.Fields["ExternalURL"];
                string externalurl = linkfield.Url;

                imageField = externalLinkItem.Fields["Image"];

                using (var client = new WebClient())
                {
                    ExternalLinksHtmlContent[count] = UnHtml(client.DownloadString(externalurl));
                }               

                doc.Add(new Field("ExternalURLContent", ExternalLinksHtmlContent[count], Field.Store.YES, Field.Index.ANALYZED));
                doc.Add(new Field("Title", externalLinkItem.Fields["Title"].Value, Field.Store.YES, Field.Index.ANALYZED));
                //doc.Add(new Field("Title", externalLinkItem.Fields["Image"].Value, Field.Store.YES, Field.Index.NOT_ANALYZED));
                doc.Add(new Field("ExternalUrl", externalurl, Field.Store.YES, Field.Index.NOT_ANALYZED));
                doc.Add(new Field("ImageSrc", Sitecore.Resources.Media.MediaManager.GetMediaUrl(imageField.MediaItem), Field.Store.YES, Field.Index.NOT_ANALYZED));
                doc.Add(new Field("ImageAlt", imageField.Alt, Field.Store.YES, Field.Index.NOT_ANALYZED));



                writer.AddDocument(doc);
                count++;
            }

            writer.Optimize();
            writer.Commit();
            writer.Dispose();
            #endregion


            return results;
        }


        public static String UnHtml(String html)
        {
            html = HttpUtility.UrlDecode(html);
            html = HttpUtility.HtmlDecode(html);

            html = RemoveTag(html, "<!--", "-->");
            html = RemoveTag(html, "<script", "</script>");
            html = RemoveTag(html, "<style", "</style>");

            //replace matches of these regexes with space
            html = _tags_.Replace(html, " ");
            html = _notOkCharacter_.Replace(html, " ");
            html = SingleSpacedTrim(html);

            return html;
        }

        private static String RemoveTag(String html, String startTag, String endTag)
        {
            Boolean bAgain;
            do
            {
                bAgain = false;
                Int32 startTagPos = html.IndexOf(startTag, 0, StringComparison.CurrentCultureIgnoreCase);
                if (startTagPos < 0)
                    continue;
                Int32 endTagPos = html.IndexOf(endTag, startTagPos + 1, StringComparison.CurrentCultureIgnoreCase);
                if (endTagPos <= startTagPos)
                    continue;
                html = html.Remove(startTagPos, endTagPos - startTagPos + endTag.Length);
                bAgain = true;
            } while (bAgain);
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
    }
}
