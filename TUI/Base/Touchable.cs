﻿using System;

namespace TUI
{
    public class Touchable : VisualDOM
    {
        #region Data

        public UILock Lock { get; set; }
        public UILock[] PersonalLock { get; set; } = new UILock[UI.MaxUsers];
        public Func<VisualObject, Touch, bool> Callback { get; set; }

        public bool Contains(Touch touch) => Contains(touch.X, touch.Y);

        #endregion

        #region Initialize

        public Touchable(int x, int y, int width, int height, UIConfiguration configuration = null, Func<VisualObject, Touch, bool> callback = null)
            : base(x, y, width, height, configuration)
        {
            Callback = callback;
        }

        #endregion
        #region Clone

        public override object Clone() =>
            new Touchable(X, Y, Width, Height, (UIConfiguration)Configuration.Clone(), Callback);

        #endregion
        #region Touched

        public virtual bool Touched(Touch touch)
        {
            if (!Active())
                throw new InvalidOperationException("Trying to call Touched on object that is not active");

            if (IsLocked(touch))
                return true;
            if (!CanTouch(touch))
                return false;

            UI.SaveTime(this, "Touched");
            bool used = TouchedChild(touch);
            UI.SaveTime(this, "Touched", "Child");
            if (!used && CanTouchThis(touch))
                used = TouchedThis(touch);
            UI.ShowTime(this, "Touched", "This");

            // TODO: This might cause problems because object can be without rootAcquire =>
            // TouchState.End touch won't proceed to this object
            if (Lock != null && touch.State == TouchState.End)
                Lock.Active = true;

            return used;
        }

        #endregion
        #region IsLocked

        public virtual bool IsLocked(Touch touch)
        {
            if (Configuration.Lock == null)
                return false;

            UILock uilock = Configuration.Lock.Type == LockType.Common ? Lock : PersonalLock[touch.Session.UserIndex];
            if (uilock != null && (DateTime.Now - uilock.Time) > TimeSpan.FromMilliseconds(uilock.Delay))
            {
                if (Configuration.Lock.Type == LockType.Common)
                    Lock = null;
                else
                    PersonalLock[touch.Session.UserIndex] = null;
                return false;
            }
            if (uilock != null &&
                (uilock.Active
                || touch.State == TouchState.Begin
                || touch.Session.Index != uilock.Touch.Session.Index))
            {
                touch.Session.Enabled = false;
                return true;
            }

            return false;
        }

        #endregion
        #region CanTouch

        public virtual bool CanTouch(Touch touch)
        {
            CanTouchArgs args = new CanTouchArgs(this as VisualObject, touch);
            UI.Hooks.CanTouch.Invoke(args);
            return !args.Handled && Configuration.CustomCanTouch?.Invoke(this as VisualObject, touch) != false;
        }

        #endregion
        #region TouchedChild

        public virtual bool TouchedChild(Touch touch)
        {
            lock (Child)
                for (int i = Child.Count - 1; i >= 0; i--)
                {
                    var o = Child[i];
                    int saveX = o.X, saveY = o.Y;
                    if (o.Enabled && o.Contains(touch))
                    {
                        touch.MoveBack(saveX, saveY);
                        if (o.Touched(touch))
                        {
                            if (Configuration.Ordered && SetTop(o))
                                PostSetTop(o);
                            return true;
                        }
                        touch.Move(saveX, saveY);
                    }
                }
            return false;
        }

        #endregion
        #region PostSetTop

        public virtual void PostSetTop(VisualObject o) { }

        #endregion
        #region CanTouchThis

        public virtual bool CanTouchThis(Touch touch) =>
            (touch.State == TouchState.Begin && Configuration.UseBegin
                || touch.State == TouchState.Moving && Configuration.UseMoving
                || touch.State == TouchState.End && Configuration.UseEnd)
            && (touch.State == TouchState.Begin || !Configuration.BeginRequire || touch.Session.BeginObject.Equals(this));

        #endregion
        #region TouchedThis

        public virtual bool TouchedThis(Touch touch)
        {
            if (touch.State == TouchState.Begin)
                touch.Session.BeginObject = this as VisualObject;

            if (Configuration.Lock != null)
            {
                UILock _lock = new UILock(this, DateTime.Now, Configuration.Lock.Delay, touch);
                if (Configuration.Lock.Level == LockLevel.Self)
                    Lock = _lock;
                else if (Configuration.Lock.Level == LockLevel.Root)
                    Root.Lock = _lock;
            }

            bool used = true;

            if (Callback != null)
            {
                UI.SaveTime(this, "invoke");
                used = Callback(this as VisualObject, touch);
                UI.ShowTime(this, "invoke", "action");
            }

            return used;
        }

        #endregion
    }
}
