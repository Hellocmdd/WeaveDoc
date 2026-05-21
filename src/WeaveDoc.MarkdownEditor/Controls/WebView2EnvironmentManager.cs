using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Threading.Tasks;

namespace WeaveDoc.MarkdownEditor.Controls
{
    /// <summary>
    /// WebView2 环境管理器，确保所有 WebView2 控件使用相同的环境
    /// </summary>
    public static class WebView2EnvironmentManager
    {
        private static CoreWebView2Environment? _sharedEnvironment;
        private static readonly object _lock = new object();

        /// <summary>
        /// 获取共享的 WebView2 环境，如果不存在则创建一个
        /// </summary>
        public static async Task<CoreWebView2Environment> GetOrCreateEnvironmentAsync()
        {
            if (_sharedEnvironment != null)
            {
                return _sharedEnvironment;
            }

            lock (_lock)
            {
                // 双重检查锁定
                if (_sharedEnvironment != null)
                {
                    return _sharedEnvironment;
                }

                // 尝试使用固定版本的 WebView2 Runtime
                string webView2Path = @"D:\Edge_BS\App";
                
                if (Directory.Exists(webView2Path))
                {
                    Console.WriteLine($"WebView2EnvironmentManager: Using fixed version from: {webView2Path}");
                    _sharedEnvironment = CoreWebView2Environment.CreateAsync(webView2Path, null, new CoreWebView2EnvironmentOptions
                    {
                        AllowSingleSignOnUsingOSPrimaryAccount = false
                    }).Result;
                }
                else
                {
                    Console.WriteLine($"WebView2EnvironmentManager: Fixed version not found at: {webView2Path}");
                    Console.WriteLine("WebView2EnvironmentManager: Trying to use system WebView2 Runtime...");
                    _sharedEnvironment = CoreWebView2Environment.CreateAsync(null, null, new CoreWebView2EnvironmentOptions
                    {
                        AllowSingleSignOnUsingOSPrimaryAccount = false
                    }).Result;
                }

                return _sharedEnvironment;
            }
        }

        /// <summary>
        /// 异步获取共享的 WebView2 环境
        /// </summary>
        public static async Task<CoreWebView2Environment> GetOrCreateEnvironmentAsync2()
        {
            if (_sharedEnvironment != null)
            {
                return _sharedEnvironment;
            }

            // 尝试使用固定版本的 WebView2 Runtime
            string webView2Path = @"D:\Edge_BS\App";
            
            if (Directory.Exists(webView2Path))
            {
                Console.WriteLine($"WebView2EnvironmentManager: Using fixed version from: {webView2Path}");
                _sharedEnvironment = await CoreWebView2Environment.CreateAsync(webView2Path, null, new CoreWebView2EnvironmentOptions
                {
                    AllowSingleSignOnUsingOSPrimaryAccount = false
                });
            }
            else
            {
                Console.WriteLine($"WebView2EnvironmentManager: Fixed version not found at: {webView2Path}");
                Console.WriteLine("WebView2EnvironmentManager: Trying to use system WebView2 Runtime...");
                _sharedEnvironment = await CoreWebView2Environment.CreateAsync(null, null, new CoreWebView2EnvironmentOptions
                {
                    AllowSingleSignOnUsingOSPrimaryAccount = false
                });
            }

            return _sharedEnvironment;
        }
    }
}
