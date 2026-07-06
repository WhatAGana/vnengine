using UnityEngine;
using NUnit.Framework;
using VNEngine.Unity;

namespace VNEngine.Tests
{
    public class SimDefinitionSOTests
    {
        [Test]
        public void ResourceDefinitionToDefMapsFields()
        {
            var so = ScriptableObject.CreateInstance<ResourceDefinitionSO>();
            so.id = "money";
            so.displayName = "재보";
            so.startValue = 100;

            ResourceDef def = so.ToDef();

            Assert.AreEqual("money", def.Id);
            Assert.AreEqual("재보", def.DisplayName);
            Assert.AreEqual(100, def.StartValue);

            Object.DestroyImmediate(so);
        }

        [Test]
        public void CommandDefinitionToDefMapsEffects()
        {
            var so = ScriptableObject.CreateInstance<CommandDefinitionSO>();
            so.id = "raid";
            so.displayName = "약탈";
            so.effects.Add(new CommandDefinitionSO.Effect { resourceId = "money", amount = 50 });
            so.effects.Add(new CommandDefinitionSO.Effect { resourceId = "magic", amount = -20 });

            CommandDef def = so.ToDef();

            Assert.AreEqual("raid", def.Id);
            Assert.AreEqual("약탈", def.DisplayName);
            Assert.AreEqual(2, def.Effects.Count);
            Assert.AreEqual("money", def.Effects[0].ResourceId);
            Assert.AreEqual(50, def.Effects[0].Amount);
            Assert.AreEqual("magic", def.Effects[1].ResourceId);
            Assert.AreEqual(-20, def.Effects[1].Amount);

            Object.DestroyImmediate(so);
        }
    }
}
