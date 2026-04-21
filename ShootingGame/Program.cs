using System.Linq;
using ShootingGame;

string[] argv = Environment.GetCommandLineArgs();
bool voxelEditor = argv.Any(static a => string.Equals(a, "--voxel-editor", StringComparison.OrdinalIgnoreCase));

if (voxelEditor)
{
    VoxelEditorApp.Run();
}
else
{
    GameApp.Run();
}
