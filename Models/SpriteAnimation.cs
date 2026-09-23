namespace DeskLofi.Models;
public sealed record SpriteAnimation(string Name, string Folder, int FrameCount, double Fps = 8, bool Loop = true, string? NextAnimation = null);
public enum GirlState { Idle, Typing, TypingFast, Mouse, Coffee, Stretch, LookWindow, Away, SitDown, LookAtCat }
public enum CatState { Idle, TailWag, Sleep, WakeUp, Stretch, LookAtGirl }
