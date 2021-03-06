﻿using System;
using System.Collections;
using System.Linq;
using System.Web.Caching;
using System.Web.Hosting;

namespace SmartStore.Web.Framework.Theming.Assets
{
    public sealed class BundlingVirtualPathProvider : ThemingVirtualPathProvider
    {
        public BundlingVirtualPathProvider(VirtualPathProvider previous)
			: base(previous)
        {
        }

        public override bool FileExists(string virtualPath)
        {
			var styleResult = ThemeHelper.IsStyleSheet(virtualPath);
			if (styleResult != null && (styleResult.IsThemeVars || styleResult.IsModuleImports))
			{
				return true;
			}

			return base.FileExists(virtualPath);
        }
         
        public override VirtualFile GetFile(string virtualPath)
        {
			var styleResult = ThemeHelper.IsStyleSheet(virtualPath);
			if (styleResult != null)
			{
				if (styleResult.IsThemeVars)
				{
					var theme = ThemeHelper.ResolveCurrentTheme();
					int storeId = ThemeHelper.ResolveCurrentStoreId();
					return new ThemeVarsVirtualFile(virtualPath, styleResult.Extension, theme.ThemeName, storeId);
				}
				else if (styleResult.IsModuleImports)
				{
					return new ModuleImportsVirtualFile(virtualPath, ThemeHelper.IsAdminArea());
				}
			}

			return base.GetFile(virtualPath);
        }
        
        public override CacheDependency GetCacheDependency(string virtualPath, IEnumerable virtualPathDependencies, DateTime utcStart)
        {
			var styleResult = ThemeHelper.IsStyleSheet(virtualPath);

			if (styleResult == null || styleResult.IsCss)
			{
				return base.GetCacheDependency(virtualPath, virtualPathDependencies, utcStart);
			}

			if (styleResult.IsThemeVars || styleResult.IsModuleImports)
			{
				return null;
			}

			// Is Sass Or Less Or StyleBundle

            var arrPathDependencies = virtualPathDependencies.Cast<string>().ToArray();

            // determine the virtual themevars.(scss|less) import reference
            var themeVarsFile = arrPathDependencies.Where(x => ThemeHelper.PathIsThemeVars(x)).FirstOrDefault();
			var moduleImportsFile = arrPathDependencies.Where(x => ThemeHelper.PathIsModuleImports(x)).FirstOrDefault();
			if (themeVarsFile == null && moduleImportsFile == null)
            {
                // no themevars or moduleimports import... so no special considerations here
				return base.GetCacheDependency(virtualPath, virtualPathDependencies, utcStart);
            }

			// exclude the special imports from the file dependencies list,
			// 'cause this one cannot be monitored by the physical file system
			var fileDependencies = arrPathDependencies
				.Except((new string[] { themeVarsFile, moduleImportsFile })
				.Where(x => x.HasValue()))
				.ToArray();

            if (fileDependencies.Any())
            {
				string cacheKey = null;

				var isThemeableAsset = (!styleResult.IsBundle && ThemeHelper.PathIsInheritableThemeFile(virtualPath))
					|| (styleResult.IsBundle && fileDependencies.Any(x => ThemeHelper.PathIsInheritableThemeFile(x)));

				if (isThemeableAsset)
				{
					var theme = ThemeHelper.ResolveCurrentTheme();
					int storeId = ThemeHelper.ResolveCurrentStoreId();
					// invalidate the cache when variables change
					cacheKey = FrameworkCacheConsumer.BuildThemeVarsCacheKey(theme.ThemeName, storeId);

					if ((styleResult.IsSass || styleResult.IsLess) && (ThemeHelper.IsStyleValidationRequest()))
					{
						// Special case: ensure that cached validation result gets nuked in a while,
						// when ThemeVariableService publishes the entity changed messages.
						return new CacheDependency(new string[0], new string[] { cacheKey }, utcStart);
					}
				}

				return new CacheDependency(
					ThemingVirtualPathProvider.MapDependencyPaths(fileDependencies), 
					cacheKey == null ? new string[0] : new string[] { cacheKey }, 
					utcStart);
            }

			return null;
        }
    }
}