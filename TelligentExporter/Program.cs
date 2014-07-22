using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using TelligentExporter.ExtensionMethods;

namespace TelligentExporter
{
    class Program
    {
        static string ReadParameterOrDefault(string message, string defaultValue)
        {
            Console.WriteLine(message + " [default: " + defaultValue + "]: ");            
            var value = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;
            else return value;
        }

        static void Main(string[] args)
        {

            Console.WriteLine("=== Telligent to Ghost blog exporter ===");
            string oldBaseUrlParameter = ReadParameterOrDefault("Blog base-url", "http://blogs.msdn.com/");
            string blogPathParameter = ReadParameterOrDefault("Blog sub-url", "b/africaapps/");
            string blogAuthorParameter = ReadParameterOrDefault("Blog author", "Ahmed Sabbour");
            string newBlogBaseUrlParameter = ReadParameterOrDefault("New blog base-url", "http://sabbour.me/");
            string newBlogImportedImageUrlParameter = ReadParameterOrDefault("New blog imported images base-url", @"/content/images/imported/");
            string workingFolderParameter = ReadParameterOrDefault("Local working folder", @"C:\BlogExport");
            

            Uri oldBlogBaseUrl = new Uri(oldBaseUrlParameter);
            Uri newBlogBaseUrl = new Uri(newBlogBaseUrlParameter);

            string exportFolder = workingFolderParameter;
            string summaryFolder = exportFolder + @"\Post Summaries";
            string postsFolder = exportFolder + @"\Posts";
            string blogPath = blogPathParameter;

            // Download the summary pages
            Console.WriteLine();
            Console.WriteLine("=== Downloading summary pages ===");
            ExportSummaryPages(oldBlogBaseUrl, blogPath, summaryFolder);

            // Download the posts and
            Console.WriteLine();
            Console.WriteLine("=== Downloading post summaries ===");
            var posts = GetPostSummaries(oldBlogBaseUrl, summaryFolder);
            if (posts != null)
            {
                Console.WriteLine("=== There are " + posts.Count + " posts on the blog ===");
                Console.WriteLine("=== Filtering posts by " + blogAuthorParameter + " ===");
                // Filter by my posts
                var myPosts = posts.Where(p => p.Author == blogAuthorParameter).ToList();
                if (myPosts != null)
                {
                    Console.WriteLine("=== There are " + myPosts.Count + " posts on the blog by " + blogAuthorParameter + " ===");
                    Console.WriteLine("=== Parsing posts ===");

                    // Populate each post's data
                    FillPostDetails(myPosts, postsFolder, oldBlogBaseUrl, newBlogBaseUrl, newBlogImportedImageUrlParameter);

                    Console.WriteLine("=== Serializing to JSON under " + exportFolder + @"\posts.json" + " ===");

                    // Serialize it into JSON
                    var jsonPosts = JsonConvert.SerializeObject(myPosts.ToList(), Formatting.Indented);

                    // Save it to a file
                    File.WriteAllText(exportFolder + @"\posts.json", jsonPosts);

                    // Convert them to Ghost format
                    var ghost = ExportToGhost(myPosts.ToList());

                    // Serialize it into JSON
                    Console.WriteLine("=== Serializing to Ghost format under " + exportFolder + @"\ghost.json" + " ===");
                    var jsonGhost = JsonConvert.SerializeObject(ghost, Formatting.Indented);

                    // Save it to a file
                    File.WriteAllText(exportFolder + @"\ghost.json", jsonGhost);
                }
            }

            Console.WriteLine("=== Done! ===");
            Console.WriteLine("Images have been downloaded to " + exportFolder + @"\Posts\Images");
            Console.WriteLine("You should upload them to to " + newBlogImportedImageUrlParameter);

            Console.ReadLine();
        }

        private static Ghost ExportToGhost(List<TelligentPost> myPosts)
        {
            var ghost = new Ghost();
            ghost.meta = new Meta();
            ghost.meta.exported_on = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
            ghost.meta.version = "000";
            ghost.data = new Data();

            // Get all the tags first
            var tagsStrings = myPosts.Where(p => p.Tags != null).SelectMany(p => p.Tags).Distinct().ToList();
            int tagId = 1;
            var tags = new List<Tag>();
            foreach (var tagString in tagsStrings)
            {
                tags.Add(new Tag
                {
                    id = tagId,
                    uuid = (Guid.NewGuid().ToString()).ToLower(),
                    name = tagString,
                    slug = tagString.ToSlug(),
                    created_at = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds,
                    created_by = 1,
                    updated_at = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds,
                    updated_by = 1
                });
                tagId++;
            }
            ghost.data.tags = tags;

            // Get all the posts
            int postId = 1;
            int postTagId = 1;
            var posts = new List<Post>();
            var postTags = new List<Posts_Tags>();

            foreach (var telligentPost in myPosts)
            {
                var post = new Post
                {
                    id = postId,
                    uuid = (Guid.NewGuid().ToString()).ToLower(),
                    page = 0,
                    author_id = 1,
                    title = telligentPost.Title,
                    slug = telligentPost.Slug,
                    markdown = telligentPost.Content,
                    html = telligentPost.Content,
                    status = "published",
                    language = "en-US",
                    created_at = (long)(telligentPost.Date - new DateTime(1970, 1, 1)).TotalMilliseconds,
                    created_by = 1,
                    updated_at = (long)(telligentPost.Date - new DateTime(1970, 1, 1)).TotalMilliseconds,
                    updated_by = 1,
                    published_at = (long)(telligentPost.Date - new DateTime(1970, 1, 1)).TotalMilliseconds,
                    published_by = 1
                };
                posts.Add(post);

                // Add the posts_tags relation now
                var thisPostTags = telligentPost.Tags;

                // For each tag, find its new id in the tags list, then add the relation
                if (thisPostTags != null)
                {
                    foreach (var tag in thisPostTags)
                    {
                        var ghostTag = ghost.data.tags.Single(t => t.name == tag);
                        var post_tag = new Posts_Tags { id = postTagId, post_id = post.id, tag_id = ghostTag.id };
                        postTags.Add(post_tag);
                        postTagId++;
                    }
                }
                postId++;
            }
            ghost.data.posts = posts;
            ghost.data.posts_tags = postTags;

            return ghost;
        }
        private static void DownloadPostImages(HtmlNode postBody, Uri oldBlogBaseUrl, string pathToDownloadInto, string importedImagePath)
        {
            var images = postBody.SelectNodes(".//img");
            if (images != null)
            {
                var links = images.Select(i => i.Attributes["src"]).ToList();
                Parallel.ForEach(images, image =>
                {
                //foreach (var image in images)
                //{
                    var src = image.GetAttributeValue("src", string.Empty);
                    if (src.StartsWith(oldBlogBaseUrl.AbsoluteUri))
                    {
                        var filename = src.Split('/').Last();
                        var fullpath = Path.Combine(pathToDownloadInto, filename);
                        EnsureOfflineFile(new Uri(src), fullpath);
                        image.Attributes["src"].Value = importedImagePath + filename;
                    }
                    });
                //}
            }
        }

        private static void DownloadPostImagesInLinks(HtmlNode postBody, Uri oldBlogBaseUrl, string pathToDownloadInto, string importedImagePath)
        {
            var links = postBody.SelectNodes(".//a");
            if (links != null)
            {
                var filtered = links.Where(a => a.GetAttributeValue("href", string.Empty).Contains("cfs-file"))
                               .Union(links.Where(a => a.GetAttributeValue("href", string.Empty).Contains("cfs-filesystemfile")))
                               .Union(links.Where(a => a.GetAttributeValue("href", string.Empty).Contains("resized-image")));

                Parallel.ForEach(filtered, link =>
                {
                    //foreach (var link in filtered)
                //{
                    // http://blogs.msdn.com/cfs-file.ashx/__key/communityserver-blogs-components-weblogfiles/00-00-01-57-37-metablogapi/2781.image_5F00_thumb_5F00_0E0A1D68.png
                    var src = link.GetAttributeValue("href", string.Empty);
                    if (src.StartsWith(oldBlogBaseUrl.AbsoluteUri))
                    {
                        var filename = src.Split('/').Last();
                        var fullpath = Path.Combine(pathToDownloadInto, filename);
                        EnsureOfflineFile(new Uri(src), fullpath);
                        link.Attributes["href"].Value = importedImagePath + filename;
                    }
                    });
                //}
            }
        }
        private static void FillPostDetails(List<TelligentPost> posts, string postsFolder, Uri oldBlogBaseUrl, Uri newBlogBaseUrl, string importedImagePath)
        {
            Parallel.ForEach(posts, post =>
             {
                 //foreach (var post in posts)
                 //{

                 var relativePath = post.RelativeUrl.Replace('/', '\\');
                 string offlinePostFilename = postsFolder + relativePath;
                 var containerfolder = Path.GetDirectoryName(offlinePostFilename);
                 var photosFolder = Path.Combine(postsFolder, "Images");
                 offlinePostFilename = Path.ChangeExtension(offlinePostFilename, ".html");

                 EnsureOfflineFile(new Uri(post.FullUrl), offlinePostFilename);

                 string html = File.ReadAllText(offlinePostFilename);

                 HtmlDocument document = new HtmlDocument();
                 document.OptionWriteEmptyNodes = true;
                 document.OptionOutputAsXml = true;
                 document.LoadHtml(html);

                 HtmlNode postDate = document.DocumentNode.SelectSingleNode("//div[@class='post-date']");

                 Debug.Assert(postDate != null);
                 post.Date = DateTime.Parse(postDate.InnerText);

                 StringBuilder buffer = new StringBuilder();
                 buffer.Append("<blockquote class='note original-post'><div><p>");
                 buffer.Append("<strong>Note: </strong>");
                 buffer.Append("This post originally appeared on my MSDN blog at ");
                 buffer.AppendFormat("\t\t<a href='{0}'>{0}</a>", post.FullUrl);
                 buffer.Append("</p></div>");
                 buffer.Append("</blockquote>");

                 HtmlNode postContent = document.DocumentNode.SelectSingleNode("//div[@class='post-content user-defined-markup']");

                 DownloadPostImages(postContent, oldBlogBaseUrl, photosFolder, importedImagePath);
                 DownloadPostImagesInLinks(postContent, oldBlogBaseUrl, photosFolder, importedImagePath);

                 Debug.Assert(postContent != null);

                 string newPostContent = TransformOriginalPostContent(postContent, post.RelativeUrl, oldBlogBaseUrl, newBlogBaseUrl, importedImagePath);
                 string content = newPostContent + buffer.ToString();

                 post.Content = content;
                 post.Tags = GetPostTags(document);

             });
                 //}
        }

        private static List<string> GetPostTags(HtmlDocument document)
        {
            HtmlNode postTagsSection = document.DocumentNode.SelectSingleNode("//div[@class='post-tags']");

            if (postTagsSection == null)
            {
                return null;
            }

            List<string> tags = new List<string>();

            IEnumerable<HtmlNode> postTags = postTagsSection.Descendants("a");

            foreach (HtmlNode link in postTags)
            {
                tags.Add(link.InnerText);
            }

            return tags;
        }

        private static void RemoveUnwantedAttributes(HtmlNode postContent, string attributeName)
        {
            HtmlNodeCollection nodes = postContent.SelectNodes("//@" + attributeName);

            if (nodes == null)
            {
                return;
            }

            foreach (HtmlNode node in nodes)
            {
                node.Attributes.Remove(attributeName);
            }
        }

        private static string TransformOriginalPostContent(HtmlNode postContent, string originalPostRelativeUrl, Uri oldBlogBaseUrl, Uri newBlogBaseUrl, string importedImagePath)
        {
            RemoveUnwantedAttributes(postContent, "mce_href");
            RemoveUnwantedAttributes(postContent, "mce_keep");
            RemoveUnwantedAttributes(postContent, "mce_src");

            //TranslateLinksToOtherBlogPosts(postContent, newBlogBaseUrl);
            TranslateLinksToOtherBlogPosts(postContent, originalPostRelativeUrl, oldBlogBaseUrl, newBlogBaseUrl, importedImagePath);

            HtmlNode lastElement = postContent.ChildNodes[postContent.ChildNodes.Count - 1];

            Debug.Assert(lastElement.Name == "div");
            Debug.Assert(lastElement.Attributes["style"].Value == "clear:both;");
            postContent.RemoveChild(lastElement);

            string newPostContent = postContent.InnerHtml;


            //if (string.IsNullOrEmpty(htmlErrors) == false)
            //{
            //    Console.WriteLine(
            //        "One or more errors detected in HTML for post ({0}):"
            //            + Environment.NewLine
            //            + "{1}",
            //        originalPostUrl,
            //        htmlErrors);
            //}

            newPostContent = newPostContent.Replace("&amp;ndash;", "--");
            newPostContent = newPostContent.Replace("&amp;ldquo;", "\"");
            newPostContent = newPostContent.Replace("&amp;rdquo;", "\"");
            newPostContent = newPostContent.Replace("&amp;rsquo;", "'");
            newPostContent = newPostContent.Replace("&amp;nbsp;", " ");

            return newPostContent;
        }

        private static void TranslateLinksToOtherBlogPosts(HtmlNode postContent, string originalPostRelativeUrl, Uri oldBlogUrl, Uri newBlogBaseUrl, string importedImagePath)
        {
            Debug.Assert(newBlogBaseUrl.AbsoluteUri.EndsWith("/") == true);

            IEnumerable<HtmlNode> nodes = postContent.Descendants("a");

            foreach (HtmlNode node in nodes)
            {
                if (node.Attributes.Contains("href") == false)
                {
                    continue;
                }
                else if (node.Attributes["href"].Value.StartsWith("#") == true)
                {
                    continue;
                }

                try
                {
                    Uri href = new Uri(node.Attributes["href"].Value);

                    if (href.AbsoluteUri.StartsWith(oldBlogUrl.AbsoluteUri, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        // Tranlsate links to images
                        if (href.AbsoluteUri.StartsWith("http://blogs.msdn.com/cfs-file.ashx") || href.AbsoluteUri.StartsWith("http://blogs.msdn.com/cfs-filesystemfile.ashx"))
                        {
                            var filename = href.AbsoluteUri.Split('/').Last();
                            var path = importedImagePath + filename;
                            Uri newHref = new Uri(newBlogBaseUrl, path);
                            node.Attributes["href"].Value = newHref.AbsolutePath;
                            continue;
                        }
                        if (href.AbsolutePath != originalPostRelativeUrl)
                        {
                            var slug = Path.GetFileNameWithoutExtension(href.AbsolutePath.Split('/').Last());
                            Uri newHref = new Uri(newBlogBaseUrl, slug);
                            node.Attributes["href"].Value = newHref.AbsolutePath;
                            continue;
                        }
                    }
                }
                catch (UriFormatException)
                {
                    // skip, do nothing
                }
            }
        }


        private static void ExportSummaryPages(Uri oldBlogBaseUrl, string blogPath, string summaryFolder)
        {
            var fullBlogUrl = new Uri(oldBlogBaseUrl, blogPath);
            Debug.Assert(fullBlogUrl.AbsoluteUri.EndsWith("/") == true);

            int yearFirstBlogPostCreated = 2013;
            int[] months = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

            for (int year = yearFirstBlogPostCreated; year <= DateTime.Now.Year; year++)
            {
                months.AsParallel().ForAll(month =>
                {
                    string summaryPageRelativeUrl = string.Format(
                       "archive/{1}/{2:D2}.aspx",
                       oldBlogBaseUrl,
                       year,
                       month);

                    Uri summaryPageUrl = new Uri(
                        fullBlogUrl,
                        summaryPageRelativeUrl);

                    string summaryPageFile = string.Format(
                        @"{0}\Monthly Summary {1}-{2:D2}.html",
                        summaryFolder,
                        year,
                        month);

                    EnsureOfflineFile(summaryPageUrl, summaryPageFile);

                    //// HACK: 28 blog posts created in October 2009 (but summary
                    //// pages only show 25 at a time)
                    //if (year == 2009 && month == 10)
                    //{
                    //    summaryPageUrl = new Uri(
                    //        oldBlogBaseUrl,
                    //        "archive/2009/10.aspx?PageIndex=2");

                    //    summaryPageFile =
                    //        summaryFolder
                    //        + @"\Monthly Summary 2009-10 Page 2.html";

                    //    EnsureOfflineFile(summaryPageUrl, summaryPageFile);
                    //}
                }
            );

            }
        }

        private static void EnsureOfflineFile(Uri url, string offlineFilename)
        {

            if (File.Exists(offlineFilename) == false)
            {
                Console.WriteLine("Downloading {0}...", url);

                string archiveFolder = Path.GetDirectoryName(offlineFilename);

                if (Directory.Exists(archiveFolder) == false)
                {
                    Directory.CreateDirectory(archiveFolder);
                }

                WebClient client = new WebClient();
                try
                {
                    client.DownloadFile(url, offlineFilename);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + ". Couldn't download " + url);
                }
            }
        }

        private static List<TelligentPost> GetPostSummaries(Uri oldBlogBaseUrl, string summaryFolder)
        {
            string[] filenames = Directory.GetFiles(summaryFolder);
            List<TelligentPost> posts = new List<TelligentPost>();

            filenames.AsParallel().ForAll(filename =>
            {
                string summaryPageContent = File.ReadAllText(filename);

                if (!summaryPageContent.Contains("No blog posts have yet been created"))
                {

                    HtmlWeb htmlWeb = new HtmlWeb();
                    HtmlDocument document = htmlWeb.Load(filename);

                    HtmlNodeCollection abbreviatedPosts =
                        document.DocumentNode.SelectNodes(
                            "//*[@class='abbreviated-post']");

                    foreach (HtmlNode abbreviatedPost in abbreviatedPosts)
                    {
                        TelligentPost post = new TelligentPost();

                        HtmlNode postHeading = abbreviatedPost.SelectSingleNode("h4[@class='post-name']");

                        Debug.Assert(postHeading != null);

                        post.Title = HttpUtility.HtmlDecode(postHeading.InnerText);

                        HtmlNode postLink = postHeading.SelectSingleNode("a");

                        Debug.Assert(postHeading != null);

                        post.RelativeUrl = postLink.Attributes["href"].Value;
                        post.FullUrl = string.Format("{0}{1}", oldBlogBaseUrl.AbsoluteUri, post.RelativeUrl.Substring(1)); // remove the first /
                        post.Slug = Path.GetFileNameWithoutExtension(post.RelativeUrl.Split('/').Last());

                        HtmlNode postSummary = abbreviatedPost.SelectSingleNode("div[@class='post-summary']");

                        Debug.Assert(postSummary != null);

                        post.Summary = postSummary.InnerText;

                        HtmlNode username = abbreviatedPost.SelectSingleNode(".//span[@class='user-name']");
                        Debug.Assert(username != null);

                        post.Author = username.InnerText;

                        posts.Add(post);
                    }
                }
            });

            return posts;
        }

    }
}
