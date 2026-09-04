using System.Collections.Generic;
using UnityEngine;
using Scripts.Core;

namespace Scripts.Combat
{
    public class CombatCommandQueue : MonoBehaviour
    {
        [SerializeField] private int maxQueueSize = 3;
        private readonly Queue<ICommand> commandQueue = new Queue<ICommand>();

        public int QueueCount => commandQueue.Count;

        public void EnqueueCommand(ICommand command)
        {
            if (command == null) return;

            if (commandQueue.Count >= maxQueueSize)
            {
                commandQueue.Dequeue(); // Drop oldest command
            }

            commandQueue.Enqueue(command);
        }

        public bool TryExecuteNextCommand()
        {
            while (commandQueue.Count > 0)
            {
                ICommand command = commandQueue.Dequeue();

                // Check for expiration if it's a BaseCombatCommand
                if (command is BaseCombatCommand bufferedCommand && bufferedCommand.IsExpired())
                {
                    continue;
                }

                if (command.CanExecute())
                {
                    command.Execute();
                    return true;
                }
            }

            return false;
        }

        public void ClearQueue()
        {
            commandQueue.Clear();
        }

        private void Update()
        {
            // Regularly prune expired commands
            if (commandQueue.Count > 0 && commandQueue.Peek() is BaseCombatCommand buffered && buffered.IsExpired())
            {
                commandQueue.Dequeue();
            }
        }
    }
}
