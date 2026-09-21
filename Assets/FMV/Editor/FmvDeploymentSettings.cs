using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FmvDemo.Editor
{
    [Serializable]
    public sealed class FmvEndpoint
    {
        public string downloadRoot = "";
        public string uploadUrl = "";
        public string uploadPassword = "";
    }

    [FilePath("UserSettings/FmvDeploymentSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class FmvDeploymentSettings : ScriptableSingleton<FmvDeploymentSettings>
    {
        [SerializeField] private FmvEndpoint test = new();
        [SerializeField] private FmvEndpoint release = new();
        public FmvEndpoint Endpoint(string profile) => profile == "Test" ? test : profile == "Release" ? release : throw new ArgumentException("Test 또는 Release 프로필이 필요합니다.");
        public void Persist() => Save(true);

        [SettingsProvider]
        public static SettingsProvider Provider()
        {
            return new SettingsProvider("Project/FMV Deployment", SettingsScope.Project)
            {
                label = "FMV Deployment",
                activateHandler = (_, root) =>
                {
                    root.style.paddingLeft = 16; root.style.paddingRight = 16; root.style.paddingTop = 16;
                    root.Add(new HelpBox("Local 데모에는 서버가 필요하지 않습니다. 각 다운로드 URL은 해당 프로필·플랫폼 파일이 위치한 폴더입니다. 설정은 이 PC의 UserSettings에 저장됩니다.", HelpBoxMessageType.Info));
                    foreach (var profile in new[] { "Test", "Release" })
                    {
                        var endpoint = instance.Endpoint(profile);
                        var heading = new Label(profile); heading.style.unityFontStyleAndWeight = FontStyle.Bold; heading.style.marginTop = 18; root.Add(heading);
                        Field(root, "Download root URL", endpoint.downloadRoot, false, v => endpoint.downloadRoot = v.Trim().TrimEnd('/'));
                        Field(root, "Upload URL", endpoint.uploadUrl, false, v => endpoint.uploadUrl = v.Trim());
                        Field(root, "Upload password", endpoint.uploadPassword, true, v => endpoint.uploadPassword = v);
                    }
                    root.Add(new HelpBox("업로드 계약: multipart 필드 file, 헤더 x-upload-password. 업로드 메뉴는 명시적으로 실행할 때만 서버에 전송합니다.", HelpBoxMessageType.None));
                }
            };
        }
        private static void Field(VisualElement root, string label, string initial, bool password, Action<string> assign)
        {
            var field = new TextField(label) { value = initial, isPasswordField = password };
            field.RegisterValueChangedCallback(change => { assign(change.newValue); instance.Persist(); });
            root.Add(field);
        }
    }
}
