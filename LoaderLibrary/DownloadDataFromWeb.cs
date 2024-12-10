using LoaderLibrary.Data;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace LoaderLibrary
{
    public class DownloadDataFromWeb
    {
        private readonly string _path;
        private readonly ILogger _log;
        private readonly string wellBoreUrl = @"https://ocd-hub-nm-emnrd.hub.arcgis.com/datasets/dd971b8e25c54d1a8ab7c549244cf3cc_0/about";

        public DownloadDataFromWeb(ILogger log, string path = @"C:\temp")
        {
            _path = path;
            _log = log;
        }

        public string DownloadWells()
        {
            string url = wellBoreUrl;
            //string file = "WELLS_SHP.ZIP";
            //string filePath = _path + "/WELLS_SHP.ZIP";
            string downloadFile = ChromeDownload(url);
            return downloadFile;
        }

        private string ChromeDownload(string url)
        {
            string downloadedFile = null;
            var chromeOptions = new ChromeOptions();
            chromeOptions.AddUserProfilePreference("download.prompt_for_download", false);
            chromeOptions.AddUserProfilePreference("download.default_directory", @"C:\temp");
            chromeOptions.AddUserProfilePreference("disable-popup-blocking", "true");

            IWebDriver driver = new ChromeDriver(chromeOptions);
            driver.Navigate().GoToUrl(url);

            var jsExecutor = (IJavaScriptExecutor)driver;

            try
            {
                WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
                driver.Manage().Window.Maximize();

                IWebElement button = wait.Until(driver =>
                {
                    var element = driver.FindElement(By.CssSelector("#main-region > div.content-hero > div.content-hero-footer > div.yielded.flex-row > button"));
                    return (element.Displayed && element.Enabled) ? element : null;
                });
                button.Click();
                _log.LogInformation("Initial button clicked successfully!");

                IWebElement shadowHost = wait.Until(driver =>
                {
                    var element = driver.FindElement(By.CssSelector("#ember42 > div > div > div > div.hub-download-list > arcgis-hub-download-list"));
                    return (element.Displayed) ? element : null;
                });

                var shadowRoot = jsExecutor.ExecuteScript("return arguments[0].shadowRoot", shadowHost);

                if (shadowRoot == null)
                {
                    _log.LogInformation("Shadow root is null. Check if the shadow host is correct and visible.");
                    return downloadedFile;
                }

                var downloadListItem = jsExecutor.ExecuteScript(
                    "return arguments[0].querySelector('arcgis-hub-download-list-item')",
                    shadowRoot) as IWebElement;

                _log.LogInformation("Download List Item Found.");

                var nestedShadowRoot = jsExecutor.ExecuteScript("return arguments[0].shadowRoot", downloadListItem);
                if (nestedShadowRoot == null)
                {
                    _log.LogInformation("Nested shadow root is null. Check the nested element.");
                    return downloadedFile;
                }
                _log.LogInformation("Nested Shadow Root Found.");

                var calciteButton = jsExecutor.ExecuteScript("return arguments[0].querySelector('calcite-button')",
                    nestedShadowRoot) as IWebElement;
                Thread.Sleep(10);
                if (calciteButton != null)
                {
                    _log.LogInformation("Calcite button found in the nested Shadow DOM.");
                    jsExecutor.ExecuteScript("arguments[0].scrollIntoView({block: 'center'});", calciteButton);
                    
                    string filePattern = "Wells_Public*.csv";
                    var oldFiles = Directory.GetFiles(_path, filePattern);
                    foreach (var file in oldFiles)
                    {
                        try
                        {
                            File.Delete(file);
                            _log.LogInformation($"Deleted old file: {file}");
                        }
                        catch (Exception ex)
                        {
                            _log.LogInformation($"Failed to delete file {file}: {ex.Message}");
                        }
                    }

                    calciteButton.Click();
                    _log.LogInformation("Calcite button clicked.");

                    _log.LogInformation("Triggered file download...");
                    int timeoutInSeconds = 60; // Maximum wait time
                    int elapsed = 0;
                    while (elapsed < timeoutInSeconds)
                    {
                        var newFiles = Directory.GetFiles(_path, filePattern)
                                                .OrderByDescending(f => File.GetCreationTime(f)) // Ensure the latest file is picked
                                                .ToList();
                        if (newFiles.Any())
                        {
                            downloadedFile = newFiles.First();
                            _log.LogInformation($"New file detected: {downloadedFile}");
                            break;
                        }
                        Thread.Sleep(1000);
                        elapsed++;
                    }

                    if (downloadedFile != null)
                    {
                        _log.LogInformation($"File downloaded successfully: {downloadedFile}");
                    }
                    else
                    {
                        _log.LogInformation("No file matching the pattern was downloaded within the timeout period.");
                    }
                }
            }
            catch (Exception e)
            {
                _log.LogInformation("An error occurred: " + e.Message);
                Exception error = new Exception(
                    "An error occurred: " + e.Message
                    );
                throw error;
                
            }
            finally
            {
                driver.Quit();
            }
            return downloadedFile;
        }
    }
}
