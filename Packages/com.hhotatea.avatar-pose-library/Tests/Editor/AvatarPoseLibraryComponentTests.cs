using System.Collections.Generic;
using com.hhotatea.avatar_pose_library.component;
using com.hhotatea.avatar_pose_library.model;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace com.hhotatea.avatar_pose_library.tests
{
    public class AvatarPoseLibraryComponentTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in objects)
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }

            objects.Clear();
        }

        [Test]
        public void GetLibraries_WithoutAvatarDescriptor_ReturnsOnlyItself()
        {
            var library = CreateObject("Library").AddComponent<AvatarPoseLibrary>();

            Assert.That(library.GetLibraries(), Is.EqualTo(new[] { library }));
        }

        [Test]
        public void GetComponentMember_FindsSameNamedLibrariesAndSelectsFirstAsLeader()
        {
            var avatar = CreateObject("Avatar");
            avatar.AddComponent<VRCAvatarDescriptor>();
            var first = CreateLibrary(avatar.transform, "First", "Shared");
            var second = CreateLibrary(avatar.transform, "Second", "Shared");
            var other = CreateLibrary(avatar.transform, "Other", "Other");

            Assert.That(second.GetComponentMember(), Is.EqualTo(new[] { first, second }));
            Assert.That(second.GetComponentLeader(), Is.SameAs(first));
            Assert.That(first.IsRootComponent(), Is.True);
            Assert.That(second.IsRootComponent(), Is.False);
            Assert.That(other.IsRootComponent(), Is.True);
        }

        [Test]
        public void GetComponentMember_WithoutData_ReturnsEmptyArray()
        {
            var library = CreateObject("Library").AddComponent<AvatarPoseLibrary>();

            Assert.That(library.GetComponentMember(), Is.Empty);
            Assert.That(library.GetComponentLeader(), Is.Null);
            Assert.That(library.IsRootComponent(), Is.False);
        }

        private AvatarPoseLibrary CreateLibrary(Transform parent, string objectName, string libraryName)
        {
            var gameObject = CreateObject(objectName);
            gameObject.transform.SetParent(parent);
            var library = gameObject.AddComponent<AvatarPoseLibrary>();
            library.data = new AvatarPoseData { name = libraryName };
            return library;
        }

        private GameObject CreateObject(string name)
        {
            var gameObject = new GameObject(name);
            objects.Add(gameObject);
            return gameObject;
        }
    }
}
