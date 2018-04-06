﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knowte.Services.I18n
{
    public interface II18nService
    {
        List<Language> GetLanguages();
        Language GetLanguage(string code);
        Language GetDefaultLanguage();
        Task ApplyLanguageAsync(string code);
        event EventHandler LanguagesChanged;
        event EventHandler LanguageChanged;
        void OnLanguagesChanged();
        void OnLanguageChanged();
    }
}
