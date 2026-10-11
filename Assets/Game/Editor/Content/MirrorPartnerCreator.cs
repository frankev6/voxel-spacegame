using System.IO;
using System.Linq;
using Game.Data.Blocks;
using Game.Data.Definitions;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Content
{
    /// <summary>Creates the X-mirror partner of a chiral block (SPEC-04 §6.4): a new asset "&lt;id&gt;_mirrored" next to the source with
    /// X-mirrored cells and clearance, ±X attach bits swapped, and both partner references set. The prop mesh is shared (mirror it by hand).</summary>
    public static class MirrorPartnerCreator
    {
        public static BlockDefinition Create(BlockDefinition source)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string id = source.IdString + "_mirrored";
            string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(Path.GetDirectoryName(sourcePath) ?? ContentDatabaseBuilder.ContentRoot, id + ".asset").Replace('\\', '/'));
            var partner = Object.Instantiate(source);
            partner.name = id;
            AssetDatabase.CreateAsset(partner, path);

            var so = new SerializedObject(partner);
            so.FindProperty("id").stringValue = id;
            Mirror(so.FindProperty("cells"));
            Mirror(so.FindProperty("clearance"));
            var faces = so.FindProperty("attachFaces");
            var f = (BlockFaceMask)faces.intValue;
            var swapped = f & ~(BlockFaceMask.PosX | BlockFaceMask.NegX);
            if ((f & BlockFaceMask.PosX) != 0) swapped |= BlockFaceMask.NegX;
            if ((f & BlockFaceMask.NegX) != 0) swapped |= BlockFaceMask.PosX;
            faces.intValue = (int)swapped;
            so.FindProperty("mirrorPartner").objectReferenceValue = source;
            so.ApplyModifiedPropertiesWithoutUndo();

            var src = new SerializedObject(source);
            src.FindProperty("mirrorPartner").objectReferenceValue = partner;
            src.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(source); EditorUtility.SetDirty(partner);
            AssetDatabase.SaveAssets();
            return partner;
        }

        static void Mirror(SerializedProperty cells)
        {
            for (int i = 0; i < cells.arraySize; i++)
            {
                var p = cells.GetArrayElementAtIndex(i);
                var v = p.vector3IntValue;
                p.vector3IntValue = new Vector3Int(-v.x, v.y, v.z);   // Sx: the root (0,0,0) stays the root, so MirrorOffset is (0,0,0)
            }
        }
    }
}
