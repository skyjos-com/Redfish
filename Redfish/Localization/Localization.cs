using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Threading;
using System.Windows.Markup;

namespace Redfish.Localization
{
    public sealed class Localization : INotifyPropertyChanged
    {
        private readonly ResourceManager resources = new ResourceManager(
            "Redfish.Localization.Strings", typeof(Localization).Assembly);
        private CultureInfo culture = CultureInfo.GetCultureInfo("en");

        public static Localization Current { get; } = new Localization();

        private Localization() { }

        public string LanguageCode => culture.Name;
        public XmlLanguage XmlLanguage => XmlLanguage.GetLanguage(culture.Name);
        public string Version => Format("Version", typeof(Localization).Assembly.GetName().Version);
        public string this[string key] => resources.GetString(key, culture) ?? key;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Format(string key, params object[] arguments)
        {
            return string.Format(culture, this[key], arguments);
        }

        public void Initialize()
        {
            string savedLanguage = null;
            try
            {
                savedLanguage = LanguagePreference.Default.Language;
            }
            catch (Exception ex) when (ex is ConfigurationErrorsException || ex is IOException ||
                ex is UnauthorizedAccessException)
            {
                Debug.WriteLine(ex);
            }
            ApplyLanguage(ResolveLanguage(savedLanguage, CultureInfo.CurrentUICulture));
        }

        internal static string ResolveLanguage(string savedLanguage, CultureInfo systemCulture)
        {
            if (IsSupportedLanguage(savedLanguage))
                return savedLanguage;
            string systemLanguage = systemCulture.TwoLetterISOLanguageName;
            if (systemLanguage == "zh")
                return "zh-Hans";
            return IsSupportedLanguage(systemLanguage) ? systemLanguage : "en";
        }

        private static bool IsSupportedLanguage(string languageCode)
        {
            return languageCode == "en" || languageCode == "zh-Hans" ||
                languageCode == "ja" || languageCode == "de";
        }

        public void SetLanguage(string languageCode)
        {
            if (!IsSupportedLanguage(languageCode))
                throw new ArgumentException("Unsupported language code.", nameof(languageCode));
            if (languageCode == LanguageCode)
                return;

            // Keep the preference per user, separate from the service's shared credentials.
            string previousLanguage = LanguagePreference.Default.Language;
            try
            {
                LanguagePreference.Default.Language = languageCode;
                LanguagePreference.Default.Save();
            }
            catch
            {
                LanguagePreference.Default.Language = previousLanguage;
                throw;
            }
            ApplyLanguage(languageCode);
        }

        private void ApplyLanguage(string languageCode)
        {
            culture = CultureInfo.GetCultureInfo(languageCode);
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            // An empty property name refreshes all bindings, including the string indexer.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }

    internal sealed class LanguagePreference : ApplicationSettingsBase
    {
        internal static LanguagePreference Default { get; } = new LanguagePreference();

        [UserScopedSetting]
        [DefaultSettingValue("")]
        public string Language
        {
            get { return (string)this[nameof(Language)]; }
            set { this[nameof(Language)] = value; }
        }
    }
}
