using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PaperText2 : MonoBehaviour
{
    [SerializeField] private TMP_Text dialogue;
    [SerializeField] private GameObject paper;

    // It may be worthwhile to collapse the following variable.
    private readonly string[] contents = new string[18]
    {
        @"THE NEXT PART 
        of this multi part scavenger hunt

        from: the game's developer",
        @"Welcome! Nice job solving the previous puzzle.

Are you ready for one more puzzle? A little bit harder this time.",
        @"Before we begin, quick reminders that:
- If you ever lose this piece of paper, come back to this table.
- There are some hints later in this pamphlet if you need them!",
        @"AND ALSO THAT:

You don't need the previous invitation anymore. But you wouldn't throw it away...right?",
        @"Here's the puzzle:

""As you might have noticed, THREE of the buildings in this vr quad...are not on the quad in real life.""",
        @"One of these buildings is your next destination.

It's the building with a small gate carved into the wall, ""for children's safety.""",
        @"",
        @"",
        @"Need a hint? Turn the page for HINT 1!",
        @"HINT 1:

The gate is carved into the wall. One of the buildings has HUGE pillars that may seem like a gate, but aren't really.",
        @"",
        @"Still stuck? Turn the page for HINT 2!",
        @"HINT 2:

How does the developer know that such an oddly specific detail (for ""children's safety"") is on the gate of the building?",
        @"",
        @"Ready for Hint 3?",
        @"HINT 3:

The building you're looking for is right next to the big LINCOLN BUST.",
        @"",
        @"Fun trivia: In real life, the building is called the Colonel Wolfe School.

It's also very, very far north of the quad."
    };

    // Update is called once per frame
    void Update()
    {
        PageNo scr = paper.GetComponent<PageNo>();

        if (scr.pageNumber > 0 && scr.pageNumber <= contents.Length)
        {
            dialogue.text = contents[scr.pageNumber - 1];
        }
        else
        {
            dialogue.text = "";
        }
    }


}
