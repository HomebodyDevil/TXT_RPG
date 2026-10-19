using System;
using NUnit.Framework;
using TxTRPG.Gameplay.Encounters;

namespace TxTRPG.Gameplay.Tests
{
    public sealed class EncounterElementSelectionTests
    {
        [Test]
        public void ToggleSetAndClear_ChangeSelection()
        {
            var selection = new EncounterElementSelection();
            Assert.That(selection.Toggle("rune"), Is.True);
            selection.SetSelected("relic", true);
            Assert.That(selection.Snapshot(), Is.EqualTo(new[] { "rune", "relic" }));
            Assert.That(selection.Toggle("rune"), Is.False);
            Assert.That(selection.IsSelected("rune"), Is.False);
            selection.Clear();
            Assert.That(selection.Count, Is.EqualTo(0));
        }

        [Test]
        public void Changed_FiresOnlyOnRealChanges()
        {
            var selection = new EncounterElementSelection();
            var changes = 0;
            selection.Changed += () => changes++;
            selection.SetSelected("rune", false);
            selection.Clear();
            Assert.That(changes, Is.EqualTo(0));
            selection.SetSelected("rune", true);
            selection.SetSelected("rune", true);
            selection.SetSelected(" rune ", true);
            Assert.That(changes, Is.EqualTo(1));
            selection.Toggle("rune");
            selection.Clear();
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void Ids_AreTrimmedAndCaseSensitive()
        {
            var selection = new EncounterElementSelection();
            selection.SetSelected("  rune ", true);
            Assert.That(selection.IsSelected("rune"), Is.True);
            Assert.That(selection.IsSelected("Rune"), Is.False);
            Assert.That(selection.Snapshot(), Is.EqualTo(new[] { "rune" }));
        }

        [Test]
        public void EmptyIds_AreRejected()
        {
            var selection = new EncounterElementSelection();
            Assert.Throws<ArgumentException>(() => selection.SetSelected("  ", true));
            Assert.Throws<ArgumentException>(() => selection.Toggle(null));
            Assert.That(selection.IsSelected(""), Is.False);
        }

        [Test]
        public void Snapshot_IsACopy()
        {
            var selection = new EncounterElementSelection();
            selection.SetSelected("rune", true);
            var snapshot = selection.Snapshot();
            selection.Clear();
            Assert.That(snapshot, Is.EqualTo(new[] { "rune" }));
        }
    }
}
