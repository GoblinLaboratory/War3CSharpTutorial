﻿using static War3Api.Common;

namespace War3Map.Template.Source
{
    internal static class Program
    {
        internal class Helpers
        {
            public static void DebugPrint(string s)
            {
            #if DEBUG
                DisplayTextToPlayer(GetLocalPlayer(), 0, 0, s);
            #endif
            }
        }

        static bool spellCondition()
        {
            // Get SpellAbilityId returns the id of the spell that was cast to activate the trigger
            return GetSpellAbilityId() == FourCC("A000");
        }

        static bool filterCondition()
        {
            // The unit being potentially filtered out of the group
            unit checkedUnit = GetFilterUnit();
            // The unit that activated the trigger, in this case the caster 
            unit caster = GetTriggerUnit();
            // Include unit in the group if its an enemy and selectable (not dead or dying)
            return IsUnitEnemy(checkedUnit, GetOwningPlayer(caster)) &&
                   BlzIsUnitSelectable(checkedUnit);
        }

        static void spellActions()
        {
            // Range around the caster that targets can be hit from 
            const float kSpellRange = 750;
            // The amount of damage each strike deals 
            const float kDamage = 250;
            // Max number of targets the spell can hit 
            const int kMaxTargets = 6;
            // variable to decrement when each target is hit
            int count = kMaxTargets;

            // Gets the unit that cast the spell associated with  
            // this trigger and saves it into a variable 
            unit caster = GetSpellAbilityUnit();
            // Gets the location of the caster 
            float startX = GetUnitX(caster);
            float startY = GetUnitY(caster);

            // Create a group variable to hold the units the spell will hit 
            group targets = CreateGroup();
            // Only the first kMaxTargets units that cause filterCondition 
            // to return true will be added to the group.
            GroupEnumUnitsInRange(targets, startX, startY, kSpellRange, Condition(filterCondition));
            // Time to play attack animation
            const float kAttackTime = 0.65f;
            // Will hit Group size units or max targets, whichever is smaller
            int numTargets = System.Math.Min(kMaxTargets, BlzGroupGetSize(targets));
            // Total time the spell should take 
            float followThroughTime = kAttackTime * numTargets;
            // Sets the spell follow through time to the calculated value 
            BlzSetAbilityRealLevelField(GetSpellAbility(), 
                                        ABILITY_RLF_FOLLOW_THROUGH_TIME, 0, followThroughTime);

            // Effect model names
            const string blinkName = @"Abilities\Spells\NightElf\Blink\BlinkCaster.mdl";
            const string shockName = @"Abilities\Spells\Items\AIlb\AIlbSpecialArt.mdl";

            // This variable will store the target we're currently hitting 
            // Start with the first unit in the group 
            unit currentTarget = FirstOfGroup(targets);

            // While there's still a target to hit and we have't yet hit max targets
            while (currentTarget != null && count > 0)
            {
                // Get start location for blink effect 
                float oldCasterX = GetUnitX(caster);
                float oldCasterY = GetUnitY(caster);
                // Create blink effect, save it to clean up later 
                effect preBlinkEffect = AddSpecialEffect(blinkName, oldCasterX, oldCasterY);

                //
                // Teleport to, face, and attack enemy 
                //
                const float twoPi = 2.0f * War3Api.Blizzard.bj_PI;
                // Get the position of the enemy we're targeting 
                float targetX = GetUnitX(currentTarget);
                float targetY = GetUnitY(currentTarget);
                // Cant occupy same spot as target. If try to, will get pushed
                // out in the same direction every time and it looks bad
                // pick a random angle and calculate an offset in that direction
                float randomOffsetAngle = GetRandomReal(0.0f, twoPi);
                const float kOffsetRadius = 50.0f;
                float offsetX = kOffsetRadius * Cos(randomOffsetAngle);
                float offsetY = kOffsetRadius * Sin(randomOffsetAngle); 
                // teleport a slight offset away from target
                SetUnitPosition(caster, targetX + offsetX, targetY + offsetY);
                // Might not be in the exact expected position
                // get position after teleport 
                float newCasterX = GetUnitX(caster);
                float newCasterY = GetUnitY(caster);

                // Spawn another blink at caster's new position
                effect postBlinkEffect = AddSpecialEffect(blinkName, newCasterX, newCasterY);

                // Get the diference between the caster and the target 
                float deltaX = targetX - newCasterX;
                float deltaY = targetY - newCasterY;
                // Take the inverse tangent of that difference vector 
                float angleInRadians = Atan2(deltaY, deltaX);
                // and convert it from radians to degrees 
                float angleInDegrees = War3Api.Blizzard.bj_RADTODEG * angleInRadians;
                // Make the caster face the calculated angle 
                SetUnitFacing(caster, angleInDegrees);
                // Have the caster play its attack animation
                SetUnitAnimation(caster, "attack");
                // Sleep to let the caster play its animation
                TriggerSleepAction(kAttackTime);

                // Have the caster deal damage to the enemy 
                UnitDamageTarget(caster, currentTarget, kDamage, true, false,
                    ATTACK_TYPE_CHAOS, DAMAGE_TYPE_NORMAL, null);

                // Create shock effect on damage attached to the target's chest
                effect shockEffect = AddSpecialEffectTarget(shockName, currentTarget, "chest");
                // Scale up shock effect 
                BlzSetSpecialEffectScale(shockEffect, 1.5f);

                // Remove the unit we just considered from the group 
                GroupRemoveUnit(targets, currentTarget);
                // Get the next unit in the group to consider. If the group is
                // empty, this will return null and break out of the while loop
                currentTarget = FirstOfGroup(targets);
                //decrement count 
                count -= 1;

                // Clean up effects 
                DestroyEffect(preBlinkEffect);
                DestroyEffect(postBlinkEffect);
                DestroyEffect(shockEffect);
            }

            // Certain Warcraft 3 types, like groups, need to be cleaned up 
            DestroyGroup(targets);
        }

        private static void Main()
        {
            // Disable Fog
            FogEnable(false);
            FogMaskEnable(false);

            //
            // Set up custom unit
            //
            // get id of custom unit                                 
            int customUnitId = FourCC("O000");
            // save spawned unit into a variable
            unit myUnit = CreateUnit(GetLocalPlayer(), customUnitId, 0.0f, 0.0f, 0.0f);
            // get id of custom ability 
            int customSpellId = FourCC("A000");
            // give custom ability to spawned unit
            UnitAddAbility(myUnit, customSpellId);

            //
            // Set up spell trigger
            //
            // Create the trigger 
            trigger spellTrigger = CreateTrigger();
            // Register the event that activates the trigger
            TriggerRegisterPlayerUnitEvent(spellTrigger, GetLocalPlayer(),
                                           EVENT_PLAYER_UNIT_SPELL_EFFECT, null);
            TriggerAddCondition(spellTrigger, Condition(spellCondition));
            TriggerAddAction(spellTrigger, spellActions);

            //
            // Spawn a circle of wisps to attack 
            //
            // The player that will own the spawned units
            player neutralAggressive = Player(PLAYER_NEUTRAL_AGGRESSIVE);
            // How many wisps to spawn 
            const uint kNumWisps = 12;
            // The radius of the circle to spawn wisps in 
            const float kCircleRadius = 500.0f;
            // The angle between each wisp 
            const float kAngleDelta = (2.0f * 3.1415f) / kNumWisps;
            // A wisp's unitId 
            int wispId = FourCC("ewsp");
            // for each wisp we want to spawn...
            for (uint i = 0; i < kNumWisps; ++i)
            {
                // Calculate position in the circle
                float angle = kAngleDelta * i;
                float x = kCircleRadius * Cos(angle);
                float y = kCircleRadius * Sin(angle);

                // Spawn wisp 
                CreateUnit(neutralAggressive, wispId, x, y, 0.0f);
            }

            Helpers.DebugPrint("Hello War3 C#!");
        }
    }
}