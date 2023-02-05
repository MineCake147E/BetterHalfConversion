using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics;
using System.Text;
using System.Numerics;

namespace BetterHalfConversion.TestsForAndroid
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);
            var w = FindViewById<TextView>(Resource.Id.textView1) ?? throw new InvalidProgramException("");
            var sb = new StringBuilder();
            sb.AppendLine($"Vector.IsHardwareAccelerated: {Vector.IsHardwareAccelerated}");
            sb.AppendLine($"Vector<byte>.Count: {Vector<byte>.Count}");
            sb.AppendLine($"Vector64.IsHardwareAccelerated: {Vector64.IsHardwareAccelerated}");
            sb.AppendLine($"Vector128.IsHardwareAccelerated: {Vector128.IsHardwareAccelerated}");
            sb.AppendLine($"Vector256.IsHardwareAccelerated: {Vector256.IsHardwareAccelerated}");
            sb.AppendLine($"AdvSimd: {AdvSimd.IsSupported}");

            var nan = BitConverter.Int32BitsToSingle(0x7f80_0001) + 1.0f;
            sb.AppendLine($"{nan}(0x{BitConverter.SingleToInt32Bits(nan):x})");
            w.Text = sb.ToString();
        }
    }
}