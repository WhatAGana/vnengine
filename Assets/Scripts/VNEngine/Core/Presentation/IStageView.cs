namespace VNEngine
{
    public interface IStageView
    {
        void SetBackground(string name);
        void ShowCharacter(string name, string position);
        void HideCharacter(string name);
    }
}
