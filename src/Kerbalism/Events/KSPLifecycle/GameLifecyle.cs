namespace KERBALISM
{
	public class GameLifecyle
	{
		public static GameLifecyle Instance { get; private set; }

		public GameLifecyle()
		{
			Instance = this;
		}

		public void OnSceneSwitchRequested(GameEvents.FromToAction<GameScenes, GameScenes> data)
		{
			PartData.ClearOnSceneSwitch();
			ModuleHandler.ClearOnSceneSwitch();
		}
	}
}
