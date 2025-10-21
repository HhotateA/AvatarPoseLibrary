using System.Collections.Generic;
using UnityEditor.Animations;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;

namespace com.hhotatea.avatar_pose_library.logic 
{
    static class AnimatorUtility
    {
        public static AnimatorStateTransition DuplicateTransition(this AnimatorState state, AnimatorStateTransition source)
        {
            var dest = state.AddTransition(source.destinationState);
            dest.hasExitTime = source.hasExitTime;
            dest.exitTime = source.exitTime;
            dest.hasFixedDuration = source.hasFixedDuration;
            dest.duration = source.duration;
            dest.offset = source.offset;
            dest.interruptionSource = source.interruptionSource;
            dest.orderedInterruption = source.orderedInterruption;
            dest.canTransitionToSelf = source.canTransitionToSelf;

            // 条件のコピー
            foreach (var condition in source.conditions)
            {
                dest.AddCondition(condition.mode, condition.threshold, condition.parameter);
            }

            return dest;
        }

        public static VRCAvatarParameterDriver AddSafeParameterDriver(this AnimatorState state)
        {
            var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            if (driver.parameters == null)
            {
                driver.parameters = new List<VRC_AvatarParameterDriver.Parameter>();
            }

            return driver;
        }
    }
}
