using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PaperText1 : MonoBehaviour
{
    [SerializeField] private TMP_Text dialogue;
    [SerializeField] private GameObject paper;

    // It may be worthwhile to collapse the following variable.
    private readonly string[] contents = new string[16]
    {
        // Don't touch the ASCII text!
        @"A QUAD DAY SCAVENGER HUNT

from: the developer
              next page
                         |
                         | 
                        V",
        @"Hi! Before we begin, you should hold on
tight to this paper.

You're definitely going to need it later.",
        @"But if you lose this paper (and any paper thereafter), come get it back at the trees where you found it!

previous page
v",
        @"As you may have realized, VR Quad day is not 100% realistic.
	
For example, I assume you're not used to walking through trees in real life!",
        @"But enough intro; LET'S BEGIN THE SCAVENGER HUNT!!

Clue: There is a tree in front of the Illini Union.",
        @"Bring this paper to the correct tree, and you'll be able to walk inside!

There you will find the next part of your scavenger hunt.",
        @"If you need any hints, read to the end.

But try to solve this puzzle without any hints. Good luck!",
        @"End of document?",
        @"",
        @"Hello! i see you noticed there was still more to this pamphlet.

Want a hint? keep reading for Hint 1!",
        @"Hint 1: What? You couldn't access the tree ON THE QUAD blocking the illini union?

Honestly, that's fine! I couldn't either...",
        @"",
        @"Still stuck? Turn the page for Hint 2!",
        @"Hint 2: The tree you need is actually BEHIND the Illini union. At least, from the quad.
	To be fair, it IS in front of the green st. entrance. :)",
        @"",
        @"(If you're still stuck, why not ask a friend where they think the illini union tree is?)"
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

// SECRET ERROR MESSAGE 

/*
If you see this, the developer probably made an error somewhere.
*/