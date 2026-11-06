using System.Collections;
using Archeus.Battle.Presentation;
using Archeus.Battle.Presentation.State;
using Archeus.Battle.Scheduling;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Archeus.Testing.Battle.Presentation
{
    public sealed class ActionOrderViewTests
    {
        [UnityTest]
        public IEnumerator DevelopmentViewSurvivesFramesWithoutBootstrapOrOwningSimulationState()
        {
            var viewObject = new GameObject("Action order view lifecycle test");
            try
            {
                var view = viewObject.AddComponent<ActionOrderDebugView>();
                yield return null;
                view.enabled = false;
                yield return null;
                view.enabled = true;
                yield return null;
                var item = new ActionOrderItem
                {
                    QueueActionID = 7,
                    SourceRuntimeID = 103,
                    TargetMode = ActionTargetMode.AllEnemies,
                    Origin = ActionOrigin.InterruptReplacement,
                    CausingExecutionID = 2,
                };
                StringAssert.Contains("#7", ActionOrderDebugView.Describe(item));
                StringAssert.Contains("AllEnemies", ActionOrderDebugView.Describe(item));
                StringAssert.Contains("cause 2", ActionOrderDebugView.Describe(item));
            }
            finally
            {
                Object.DestroyImmediate(viewObject);
            }
        }
    }
}
