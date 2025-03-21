// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System.Linq;

namespace UnityEditor.PackageManager.UI.Internal
{
    internal class InstallFoldoutGroup : MultiSelectFoldoutGroup
    {
        public InstallFoldoutGroup(ApplicationProxy applicationProxy, PackageDatabase packageDatabase)
            : base(new PackageAddButton(applicationProxy, packageDatabase), null)
        {
        }

        public override void Refresh()
        {
            if (mainFoldout.versions.FirstOrDefault()?.HasTag(PackageTag.BuiltIn) == true)
                mainFoldout.headerTextTemplate = L10n.Tr("Enable {0}");
            else
                mainFoldout.headerTextTemplate = L10n.Tr("Install {0}");

            if (inProgressFoldout.versions.FirstOrDefault()?.HasTag(PackageTag.BuiltIn) == true)
                inProgressFoldout.headerTextTemplate = L10n.Tr("Enabling {0}");
            else
                inProgressFoldout.headerTextTemplate = L10n.Tr("Installing {0}");

            base.Refresh();
        }
    }
}
