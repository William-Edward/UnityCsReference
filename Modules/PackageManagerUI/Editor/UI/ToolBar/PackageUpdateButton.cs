// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System.Collections.Generic;
using System.Linq;

namespace UnityEditor.PackageManager.UI.Internal
{
    internal class PackageUpdateButton : PackageToolBarRegularButton
    {
        private static readonly string k_MultiSelectUpdateButtonText = L10n.Tr("Update");
        private static readonly string k_MultiSelectUpdatingButtonText = L10n.Tr("Updating");
        private static readonly string k_UpdateToButtonTextFormat = L10n.Tr("Update to {0}");
        private static readonly string k_UpdatingToButtonTextFormat = L10n.Tr("Updating to {0}");

        internal static IPackageVersion GetTargetVersion(IPackageVersion version)
        {
            if (version?.isInstalled == true && version != version.package.versions.recommended)
                return version.package.versions.latest ?? version;
            return version;
        }

        private ApplicationProxy m_Application;
        private PackageDatabase m_PackageDatabase;
        private PageManager m_PageManager;
        public PackageUpdateButton(ApplicationProxy applicationProxy,
                                PackageDatabase packageDatabase,
                                PageManager pageManager)
        {
            m_Application = applicationProxy;
            m_PackageDatabase = packageDatabase;
            m_PageManager = pageManager;
        }



        protected override bool TriggerAction(IList<IPackageVersion> versions)
        {
            m_PackageDatabase.Install(versions.Select(v => GetTargetVersion(v)));
            // The current multi-select UI does not allow users to install non-recommended versions
            // Should this change in the future, we'll need to update the analytics event accordingly.
            PackageManagerWindowAnalytics.SendEvent("installUpdateRecommended", packageIds: versions.Select(v => v.uniqueId));
            return true;
        }

        protected override bool TriggerAction(IPackageVersion version)
        {
            var installedVersion = version.package.versions.installed;
            var targetVersion = GetTargetVersion(version);
            if (installedVersion != null && !installedVersion.isDirectDependency && installedVersion != targetVersion)
            {
                var featureSetDependents = m_PackageDatabase.GetFeatureDependents(installedVersion);
                // if the installed version is being used by a Feature Set show the more specific
                //  Feature Set dialog instead of the generic one
                if (featureSetDependents.Any())
                {
                    var message = string.Format(L10n.Tr("Changing a {0} that is part of a feature can lead to errors. Are you sure you want to proceed?"), version.package.GetDescriptor());
                    if (!m_Application.DisplayDialog(L10n.Tr("Warning"), message, L10n.Tr("Yes"), L10n.Tr("No")))
                        return false;
                }
                else
                {
                    var message = L10n.Tr("This version of the package is being used by other packages. Upgrading a different version might break your project. Are you sure you want to continue?");
                    if (!m_Application.DisplayDialog(L10n.Tr("Unity Package Manager"), message, L10n.Tr("Yes"), L10n.Tr("No")))
                        return false;
                }
            }

            IPackage[] packageToUninstall = null;
            if (targetVersion.HasTag(PackageTag.Feature))
            {
                var customizedDependencies = m_PackageDatabase.GetCustomizedDependencies(targetVersion, true);
                if (customizedDependencies.Any())
                {
                    var packageNameAndVersions = string.Join("\n\u2022 ",
                        customizedDependencies.Select(package => $"{package.displayName} - {package.versions.lifecycleVersion.version}").ToArray());

                    var message = customizedDependencies.Length == 1 ?
                        string.Format(
                        L10n.Tr("This {0} includes a package version that is different from what's already installed. Would you like to reset the following package to the required version?\n\u2022 {1}"),
                        version.package.GetDescriptor(), packageNameAndVersions) :
                        string.Format(
                        L10n.Tr("This {0} includes package versions that are different from what are already installed. Would you like to reset the following packages to the required versions?\n\u2022 {1}"),
                        version.package.GetDescriptor(), packageNameAndVersions);

                    var result = m_Application.DisplayDialogComplex(L10n.Tr("Unity Package Manager"), message, L10n.Tr("Install and Reset"), L10n.Tr("Cancel"), L10n.Tr("Install Only"));
                    if (result == 1) // Cancel
                        return false;
                    if (result == 0) // Install and reset
                        packageToUninstall = customizedDependencies;
                }
            }

            if (packageToUninstall?.Any() == true)
            {
                m_PackageDatabase.InstallAndResetDependencies(targetVersion, packageToUninstall);
                PackageManagerWindowAnalytics.SendEvent("installAndReset", targetVersion?.uniqueId);
            }
            else
            {
                m_PackageDatabase.Install(targetVersion);

                var installRecommended = version.package.versions.recommended == targetVersion ? "Recommended" : "NonRecommended";
                var eventName = $"installUpdate{installRecommended}";
                PackageManagerWindowAnalytics.SendEvent(eventName, targetVersion?.uniqueId);
            }
            return true;
        }

        protected override bool IsVisible(IPackageVersion version)
        {
            var installed = version?.package.versions.installed;
            var targetVersion = GetTargetVersion(version);
            return installed?.HasTag(PackageTag.VersionLocked) == false
                && targetVersion?.HasTag(PackageTag.Installable) == true
                && installed != targetVersion
                && !version.IsRequestedButOverriddenVersion
                && !version.HasTag(PackageTag.Local)
                && m_PageManager.GetVisualState(version.package)?.isLocked != true;
        }

        protected override string GetTooltip(IPackageVersion version, bool isInProgress)
        {
            if (isInProgress)
                return k_InProgressGenericTooltip;

            return string.Format(L10n.Tr("Click to update this {0} to the specified version."), version.package.GetDescriptor());
        }

        protected override string GetText(IPackageVersion version, bool isInProgress)
        {
            if (m_PageManager.GetSelection().Count > 1)
                return isInProgress ? k_MultiSelectUpdatingButtonText : k_MultiSelectUpdateButtonText;

            return string.Format(isInProgress ? k_UpdatingToButtonTextFormat : k_UpdateToButtonTextFormat, GetTargetVersion(version).version);
        }

        protected override bool IsInProgress(IPackageVersion version) => m_PackageDatabase.IsInstallInProgress(GetTargetVersion(version));

        protected override IEnumerable<ButtonDisableCondition> GetDisableConditions(IPackageVersion version)
        {
            yield return new ButtonDisableCondition(() => version?.package.hasEntitlementsError ?? false,
                L10n.Tr("You need to sign in with a licensed account to perform this action."));
        }
    }
}
