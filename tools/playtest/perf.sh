#!/bin/zsh
# perf.sh  One line of frame timing / render / memory stats, plus voxel entity counts.
. "$(dirname "$0")/_env.sh"
"$PT_DIR/g.sh" get_performance_stats | python3 -c "
import json,sys
try: d=json.load(sys.stdin)
except Exception as e: print('perf stats unavailable:', e); sys.exit()
f=d['frameTiming']; r=d['render']; m=d['memory']
print(f\"cpu={f['cpuFrameTimeMs']:.1f}ms main={f['cpuMainThreadFrameTimeMs']:.1f}ms gpu={f['gpuFrameTimeMs']:.1f}ms draws={r['drawCalls']} setpass={r['setPassCalls']} tris={r['triangles']/1e6:.2f}M mono={m['monoUsedBytes']/1e6:.0f}MB\")"
"$PT_DIR/g.sh" ecs --filter Voxel --max 6
