namespace GG.App.ViewModels;

/// <summary>Actions a game card can ask for; implemented by <see cref="MainViewModel"/>.</summary>
public interface IGameActions
{
    void Play(GameViewModel game);

    void ToggleFavorite(GameViewModel game);
}
