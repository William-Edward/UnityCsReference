// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.UIElements
{
    static partial class UIElementsTemplate
    {
        private static string GetCurrentFolder()
        {
            string filePath;
            if (Selection.assetGUIDs.Length == 0)
            {
                // No asset selected.
                filePath = "Assets";
            }
            else
            {
                // Get the path of the selected folder or asset.
                filePath = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);

                // If the path points to a file, asset should be created in the directory that file is in
                if (File.Exists(filePath))
                {
                    filePath = Path.GetDirectoryName(filePath);
                }
            }

            return filePath;
        }

        [MenuItem("Assets/Create/UI Toolkit/UI Document", false, 610, false)]
        private static void CreateUXMAsset()
        {
            var folder = GetCurrentFolder();
            var contents = CreateUXMLTemplate(folder);
            var icon = EditorGUIUtility.IconContent<VisualTreeAsset>().image as Texture2D;
            ProjectWindowUtil.CreateAssetWithContent("NewUXMLTemplate.uxml", contents, icon);
        }

        public static string CreateUXMLTemplate(string folder, string uxmlContent = "")
        {
            UxmlSchemaGenerator.UpdateSchemaFiles(true);

            string[] pathComponents = folder.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> backDots = new List<string>();
            foreach (var s in pathComponents)
            {
                if (s == ".")
                {
                    continue;
                }
                if (s == ".." && backDots.Count > 0)
                {
                    backDots.RemoveAt(backDots.Count - 1);
                }
                else
                {
                    backDots.Add("..");
                }
            }
            backDots.Add(UxmlSchemaGenerator.k_SchemaFolder);
            string schemaDirectory = string.Join("/", backDots.ToArray());

            string xmlnsList = String.Empty;
            Dictionary<string, string> namespacePrefix = UxmlSchemaGenerator.GetNamespacePrefixDictionary();

            foreach (var prefix in namespacePrefix)
            {
                if (prefix.Key == String.Empty)
                    continue;

                if (prefix.Value != String.Empty)
                {
                    xmlnsList += "    xmlns:" + prefix.Value + "=\"" + prefix.Key + "\"\n";
                }
            }

            string uxmlTemplate = String.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
<engine:{0}
    xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
{1}    xsi:noNamespaceSchemaLocation=""{2}/UIElements.xsd""
>
    {3}
</engine:{0}>", UXMLImporterImpl.k_RootNode, xmlnsList, schemaDirectory, uxmlContent);

            return uxmlTemplate;
        }
    }
}
