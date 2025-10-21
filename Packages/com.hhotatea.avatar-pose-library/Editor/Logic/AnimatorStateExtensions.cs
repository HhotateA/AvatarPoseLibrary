using System.Collections.Generic;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;

namespace com.hhotatea.avatar_pose_library.logic {
    static class AnimatorStateExtensions {
        public static VRCAvatarParameterDriver AddSafeParameterDriver(this AnimatorState state) {
            var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            if (driver.parameters == null) {
                driver.parameters = new List<VRC_AvatarParameterDriver.Parameter>();
            }

            return driver;
        }
    }
}
