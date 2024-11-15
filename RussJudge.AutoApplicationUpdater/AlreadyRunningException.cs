namespace RussJudge.AutoApplicationUpdater
{
    public class AlreadyRunningException : Exception
    {
        public AlreadyRunningException() : base("A copy of this application is already running.") { }
    }
}
