namespace OpenLauncher.UI.Services;

public interface IAppLifetime
{
    bool IsExiting { get; }

    void RequestExit();
}
