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
        private static Task<CoreWebView2Environment>? _creationTask;

        /// <summary>
        /// 获取共享的 WebView2 环境，如果不存在则创建一个
        /// </summary>
        public static Task<CoreWebView2Environment> GetOrCreateEnvironmentAsync()
        {
            if (_sharedEnvironment != null)
            {
                return Task.FromResult(_sharedEnvironment);
            }

            lock (_lock)
            {
                if (_sharedEnvironment != null)
                {
                    return Task.FromResult(_sharedEnvironment);
                }

                // 如果已有创建任务正在进行，等待它完成
                if (_creationTask != null)
                {
                    return _creationTask;
                }

                // 开始异步创建环境
                _creationTask = CreateEnvironmentAsync();
                return _creationTask;
            }
        }

        private static async Task<CoreWebView2Environment> CreateEnvironmentAsync()
        {
            try
            {
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
            finally
            {
                _creationTask = null;
            }
        }

        /// <summary>
        /// 异步获取共享的 WebView2 环境（简化版本）
        /// </summary>
        public static Task<CoreWebView2Environment> GetOrCreateEnvironmentAsync2()
        {
            return GetOrCreateEnvironmentAsync();
        }
    }
}