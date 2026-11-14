using System.Collections.Generic;

/// <summary>Offline instructions verified against QuadProjectAlexanderLau's authored gameplay.</summary>
public static class QuadGameGuideCatalog
{
    // Landmark names are exact keys in Map_Settings and Quad_WaypointMap.
    // This guide never starts games, moves the player, or changes puzzle progress.
    public static IReadOnlyList<QuadGameGuide> Guides { get; } = System.Array.AsReadOnly(new[] {
        new QuadGameGuide("archery", "Archery", "Archery Range", "map",
            new QuadGameGuideStep("Aim for the center", "Visit the firing plate at the Archery Range. Take the bow and an arrow from the quiver. Your goal is to hit the target rings and improve your score."),
            new QuadGameGuideStep("Nock, draw, release", "Hold the bow in one hand. Bring an arrow to the socket on its string. With your other hand, grab and pull the string back, aim at the target, then release the string to shoot."),
            new QuadGameGuideStep("Check your score", "The rings award 10, 8, 6, 4 or 2 points, from the center outward. Check the range scoreboard, take another arrow and try again. Show on map selects the firing plate; choose Navigate to follow the route."),
            new QuadGameGuideStep("Recover the equipment", "To restore the quiver and arrows, put a controller inside the range's reset button area and press the right index trigger. This resets the equipment, not your score.")),
        new QuadGameGuide("mammoth", "Mammoth Tag", "Mammoth Tag", "map",
            new QuadGameGuideStep("Find the golden donut", "Start at the golden-donut pedestal and warning sign. The mammoth is interested in the donut. Approaching within about two meters can start the chase, even before you pick it up."),
            new QuadGameGuideStep("Keep the donut moving", "Grab the golden donut and use your usual movement controls to get away. The mammoth follows the donut. Getting caught while holding it, or the mammoth reaching the donut, ends the chase."),
            new QuadGameGuideStep("Know where to start", "The map leads to the sign and pedestal, not the roaming mammoth. This guide does not restart a finished chase. You can choose another game from the guide when the chase ends.")),
        new QuadGameGuide("blocks", "Block Snap", "Block Snap Game", "tutorials",
            new QuadGameGuideStep("Build the letter wall", "Find the loose letter pieces beside the Block Snap wall. Assemble each letter, then place it in its matching slot. Complete all six letters to trigger the celebration."),
            new QuadGameGuideStep("Match the connectors", "Grab a top or bottom piece and align its snap point with the matching middle. Use pieces of the same letter type. L and N pieces have specific top and bottom connectors."),
            new QuadGameGuideStep("Place the whole letter", "Once both pieces are attached, carry the assembly by its middle. Align it with the matching wall slot. Installed letters lock in place. Continue with the remaining letters; there is no reset button in this guide.")),
        new QuadGameGuide("parkour", "Parkour", "Parkour Entrance", "up",
            new QuadGameGuideStep("Reach the course", "Follow the map to the ground-level ladder at Parkour Entrance. Use the movement stick to travel and A to jump. Reach checkpoint platforms as you move through the course. These controls use Quest controllers."),
            new QuadGameGuideStep("Climb the ladder", "At a ladder, hold A to climb up or B to climb down. Press X to vault off. Falling into the course's catch area returns you to your selected checkpoint."),
            new QuadGameGuideStep("Choose a checkpoint", "At a Waystone, Y increases the stage and X decreases it. B switches between steps of 1 and 10. A confirms a stage you have already reached. This changes your respawn point; it does not teleport you immediately."),
            new QuadGameGuideStep("Beat the countdown", "The tower challenge begins when you enter its start platform. Reach the finish before the countdown expires. Running out of time sends you back to the challenge start. The map guides you to the entrance, not through each jump.")),
        new QuadGameGuide("scavenger", "Scavenger Hunt", "Scavenger Hunt Start", "tutorials",
            new QuadGameGuideStep("Follow the paper trail", "Go to the first table at Scavenger Hunt Start. Pick up the paper and follow its clues through the campus. Carry the current paper with you. Later clues and rewards unlock as you complete earlier stages."),
            new QuadGameGuideStep("Read and turn pages", "Keep the paper upright to reveal its writing. Use the controller in the hand holding it: right hand B is next and A is previous; left hand Y is next and X is previous. Read the clue before moving on."),
            new QuadGameGuideStep("Use the checklist", "Hold the third paper open to page 10. Approach the red sensors in the clue's order while holding it. Completed sensors turn green. The map leads to the start; follow the clues to solve the puzzle.")),
        new QuadGameGuide("rings", "Alma Ring Toss", "Alma Mater", "map",
            new QuadGameGuideStep("Meet Alma Mater", "Visit Alma Mater and find the nearby hoop table. Grab a hoop and aim toward the statue's target areas. The Alma scoreboard tracks your successful hits."),
            new QuadGameGuideStep("Toss a hoop", "Hold the hoop with your usual grab control. Swing your hand toward a target and release. A hoop passing across a scoring target adds one point. Try different throws and watch the scoreboard."),
            new QuadGameGuideStep("Try another throw", "Look back at the hoop table for replacement hoops after a throw. Show on map selects the clear approach beside Alma Mater. Choose Navigate for the route, then continue to the nearby table."))
    });
}

public sealed class QuadGameGuide
{
    public string Id { get; }
    public string Title { get; }
    public string Landmark { get; }
    public string Symbol { get; }
    public IReadOnlyList<QuadGameGuideStep> Steps { get; }
    public QuadGameGuide(string id, string title, string landmark, string symbol, params QuadGameGuideStep[] steps)
    { Id = id; Title = title; Landmark = landmark; Symbol = symbol; Steps = System.Array.AsReadOnly(steps); }
}

public sealed class QuadGameGuideStep
{
    public string Title { get; }
    public string Body { get; }
    public QuadGameGuideStep(string title, string body) { Title = title; Body = body; }
}
