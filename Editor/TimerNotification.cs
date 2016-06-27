//
//namespace Core
//{
//    using System.Threading.Tasks;
//
//    public class TimerNotification
//    {
//        private TaskCompletionSource<TimerNotification> tcs = new TaskCompletionSource<TimerNotification>();
//
//        public Task<TimerNotification> GetNext()
//        {
//            return tcs.Task;
//        }
//
//        public void SetNext(TimerNotification next)
//        {
//            tcs.SetResult(next);
//        }
//    }
//
//}
