using System;

[Serializable]
public struct InteractablePromptData
{
    public string actionText;
    public int woodCost;
    public int scrapCost;
    public bool canAfford;

    public static InteractablePromptData CreateSimple(string actionText)
    {
        return new InteractablePromptData
        {
            actionText = actionText,
            woodCost = 0,
            scrapCost = 0,
            canAfford = true
        };
    }
}
