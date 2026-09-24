namespace DeskLofi.Models;
public sealed record SpriteAnimation(string Name, string Folder, int FrameCount, double Fps = 8, bool Loop = true, string? NextAnimation = null);
public enum GirlState { Idle, Typing, TypingFast, Mouse, Coffee, Stretch, LookWindow, Away, SitDown, LookAtCat }
public enum UserActivityState { Idle, Typing, Mouse }
public enum GirlAction { None, Coffee, Stretch }
public enum CatState { Idle, TailWag, GoingToSleep, Sleeping, WakingUp }
