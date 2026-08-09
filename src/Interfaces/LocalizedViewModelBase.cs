using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Messages;

namespace DLSS_Swapper.Interfaces;

public abstract class LocalizedViewModelBase : ObservableObject, IDisposable
{
    bool _disposed;

    public LocalizedViewModelBase()
    {
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(
            this,
            static (recipient, _) => ((LocalizedViewModelBase)recipient).OnLanguageChanged());
    }

    protected virtual void OnLanguageChanged()
    {
        var currentClassType = GetType();
        var languageProperties = LanguageManager.GetClassLanguagePropertyNames(currentClassType);
        foreach (var propertyName in languageProperties)
        {
            OnPropertyChanged(propertyName);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            WeakReferenceMessenger.Default.Unregister<LanguageChangedMessage>(this);
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
