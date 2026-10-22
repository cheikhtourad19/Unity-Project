using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.iOS;
using UnityEngine;

namespace HeritageXR.Editor
{
    public sealed class BrandingBuildPreparation : IPreprocessBuildWithReport
    {
        const string Folder = "Assets/HeritageXR/Resources/Branding/";
        public int callbackOrder => -900;
        public void OnPreprocessBuild(BuildReport report) => Configure();

        public static void Configure()
        {
            ConfigureTexture(Folder + "AppIcon.png"); ConfigureTexture(Folder + "SplashBackground.png");
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "AppIcon.png");
            if (icon == null) throw new Exception("HeritageXR app icon is missing.");
            var platform = NamedBuildTarget.iOS;
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(platform))
            {
                var icons = PlayerSettings.GetPlatformIcons(platform, kind);
                foreach (var slot in icons) slot.SetTexture(icon);
                PlayerSettings.SetPlatformIcons(platform, kind, icons);
            }
            PlayerSettings.SplashScreen.show = false;
            AssetDatabase.SaveAssets();
        }
        static void ConfigureTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new Exception("Branding texture missing: " + path);
            if (importer.mipmapEnabled || importer.alphaSource != TextureImporterAlphaSource.None || importer.maxTextureSize != 2048 || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false; importer.alphaSource = TextureImporterAlphaSource.None;
                importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None; importer.SaveAndReimport();
            }
        }

        [PostProcessBuild(100)]
        public static void BrandNativeLaunch(BuildTarget target, string output)
        {
            if (target != BuildTarget.iOS) return;
            // Keep Unity's resource references and launch names; replace their contents.
            foreach (string file in new[] { "LaunchScreen-iPhonePortrait.png", "LaunchScreen-iPhoneLandscape.png", "LaunchScreen-iPad.png" })
            {
                string destination = Path.Combine(output, file);
                if (!File.Exists(destination)) throw new Exception("Expected Unity launch resource is missing: " + file);
                File.Copy(Folder + "SplashBackground.png", destination, true);
            }
            File.WriteAllText(Path.Combine(output, "LaunchScreen-iPhone.storyboard"), LaunchStoryboard("LaunchScreen-iPhonePortrait.png"));
            File.WriteAllText(Path.Combine(output, "LaunchScreen-iPad.storyboard"), LaunchStoryboard("LaunchScreen-iPad.png"));
        }
        static string LaunchStoryboard(string image) => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<document type=""com.apple.InterfaceBuilder3.CocoaTouch.Storyboard.XIB"" version=""3.0"" toolsVersion=""23501"" targetRuntime=""iOS.CocoaTouch"" propertyAccessControl=""none"" useAutolayout=""YES"" launchScreen=""YES"" useTraitCollections=""YES"" useSafeAreas=""YES"" colorMatched=""YES"" initialViewController=""launch-controller"">
  <device id=""retina6_12"" orientation=""portrait"" appearance=""light""/>
  <dependencies><plugIn identifier=""com.apple.InterfaceBuilder.IBCocoaTouchPlugin"" version=""23501""/><capability name=""Safe area layout guides"" minToolsVersion=""9.0""/><capability name=""documents saved in the Xcode 8 format"" minToolsVersion=""8.0""/></dependencies>
  <scenes><scene sceneID=""launch-scene""><objects>
    <viewController id=""launch-controller"" sceneMemberID=""viewController""><view key=""view"" contentMode=""scaleToFill"" id=""launch-root"">
      <rect key=""frame"" x=""0"" y=""0"" width=""393"" height=""852""/>
      <subviews>
        <imageView clipsSubviews=""YES"" userInteractionEnabled=""NO"" contentMode=""scaleAspectFill"" image=""" + image + @""" translatesAutoresizingMaskIntoConstraints=""NO"" id=""launch-art""><rect key=""frame"" x=""0"" y=""0"" width=""393"" height=""852""/></imageView>
        <label opaque=""NO"" userInteractionEnabled=""NO"" contentMode=""left"" text=""HeritageXR"" textAlignment=""center"" adjustsFontSizeToFit=""YES"" minimumScaleFactor=""0.7"" translatesAutoresizingMaskIntoConstraints=""NO"" id=""launch-title""><rect key=""frame"" x=""24"" y=""100"" width=""345"" height=""54""/><fontDescription key=""fontDescription"" type=""system"" weight=""semibold"" pointSize=""38""/><color key=""textColor"" red=""0.96"" green=""0.92"" blue=""0.81"" alpha=""1"" colorSpace=""custom"" customColorSpace=""sRGB""/></label>
        <label opaque=""NO"" userInteractionEnabled=""NO"" contentMode=""left"" text=""Projet académique réalisé par Mohamed Elgheith, étudiant à esprim."" textAlignment=""center"" numberOfLines=""0"" translatesAutoresizingMaskIntoConstraints=""NO"" id=""launch-credit""><rect key=""frame"" x=""28"" y=""700"" width=""337"" height=""84""/><fontDescription key=""fontDescription"" type=""system"" pointSize=""18""/><color key=""textColor"" red=""0.96"" green=""0.92"" blue=""0.81"" alpha=""1"" colorSpace=""custom"" customColorSpace=""sRGB""/></label>
      </subviews>
      <viewLayoutGuide key=""safeArea"" id=""launch-safe""/>
      <color key=""backgroundColor"" red=""0.125"" green=""0.227"" blue=""0.196"" alpha=""1"" colorSpace=""custom"" customColorSpace=""sRGB""/>
      <constraints>
        <constraint firstItem=""launch-art"" firstAttribute=""top"" secondItem=""launch-root"" secondAttribute=""top"" id=""art-top""/>
        <constraint firstItem=""launch-art"" firstAttribute=""bottom"" secondItem=""launch-root"" secondAttribute=""bottom"" id=""art-bottom""/>
        <constraint firstItem=""launch-art"" firstAttribute=""leading"" secondItem=""launch-root"" secondAttribute=""leading"" id=""art-leading""/>
        <constraint firstItem=""launch-art"" firstAttribute=""trailing"" secondItem=""launch-root"" secondAttribute=""trailing"" id=""art-trailing""/>
        <constraint firstItem=""launch-title"" firstAttribute=""top"" secondItem=""launch-safe"" secondAttribute=""top"" constant=""54"" id=""title-top""/>
        <constraint firstItem=""launch-title"" firstAttribute=""leading"" secondItem=""launch-safe"" secondAttribute=""leading"" constant=""24"" id=""title-leading""/>
        <constraint firstItem=""launch-safe"" firstAttribute=""trailing"" secondItem=""launch-title"" secondAttribute=""trailing"" constant=""24"" id=""title-trailing""/>
        <constraint firstItem=""launch-title"" firstAttribute=""height"" constant=""54"" id=""title-height""/>
        <constraint firstItem=""launch-credit"" firstAttribute=""leading"" secondItem=""launch-safe"" secondAttribute=""leading"" constant=""28"" id=""credit-leading""/>
        <constraint firstItem=""launch-safe"" firstAttribute=""trailing"" secondItem=""launch-credit"" secondAttribute=""trailing"" constant=""28"" id=""credit-trailing""/>
        <constraint firstItem=""launch-safe"" firstAttribute=""bottom"" secondItem=""launch-credit"" secondAttribute=""bottom"" constant=""44"" id=""credit-bottom""/>
      </constraints>
    </view></viewController>
    <placeholder placeholderIdentifier=""IBFirstResponder"" id=""launch-responder"" userLabel=""First Responder"" sceneMemberID=""firstResponder""/>
  </objects></scene></scenes>
  <resources><image name=""" + image + @""" width=""1024"" height=""1792""/></resources>
</document>";
    }
}
