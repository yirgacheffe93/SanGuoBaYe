using Godot;

namespace Game.Godot;

public sealed class MainScreenNavigator(Control gameRoot, MainBattlePanel battlePanel, MainStartScreen startScreen, MainSavePanel savePanel)
{
    public void ShowGame()
    {
        startScreen.Hide();
        savePanel.Hide();
        battlePanel.Hide();
        gameRoot.Show();
    }

    public void ShowBattle()
    {
        gameRoot.Hide();
        battlePanel.Show();
    }

    public void ShowTitle(bool canContinue, bool hasSession)
    {
        gameRoot.Hide();
        battlePanel.Hide();
        savePanel.Hide();
        startScreen.SetContinue(canContinue, hasSession);
        startScreen.Show();
    }

    public void ShowSaves() => savePanel.Show();
    public void CloseSaves(bool canContinue, bool hasSession)
    {
        savePanel.Hide();
        startScreen.SetContinue(canContinue, hasSession);
    }
}
