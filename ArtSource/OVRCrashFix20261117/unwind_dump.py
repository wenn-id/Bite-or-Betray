"""Read a Windows x64 minidump; map PE bytes as data for the OS unwinder, never execute them."""
from pathlib import Path
import bisect
import ctypes as c
import struct
import sys
import pefile

path = Path(sys.argv[1])
data = path.read_bytes()
u32 = lambda off: struct.unpack_from('<I', data, off)[0]
u64 = lambda off: struct.unpack_from('<Q', data, off)[0]
streams = {t: (off, size) for t, size, off in
           (struct.unpack_from('<III', data, u32(12) + i * 12) for i in range(u32(8)))}
modules = []
off = streams[4][0]
for i in range(u32(off)):
    at = off + 4 + 108 * i
    nameoff = u32(at + 20)
    name = data[nameoff + 4:nameoff + 4 + u32(nameoff)].decode('utf-16-le')
    modules.append((u64(at), u64(at) + u32(at + 8), name))
modules.sort()
bases = [m[0] for m in modules]
at = streams[6][0]
thread = u32(at)
ctxsize, ctxoff = struct.unpack_from('<II', data, at + 160)
context = c.create_string_buffer(max(ctxsize, 4096))
c.memmove(context, data[ctxoff:ctxoff + ctxsize], ctxsize)
reg = lambda offset: struct.unpack_from('<Q', context, offset)[0]
toff = streams[3][0]
stack = None
for i in range(u32(toff)):
    at = toff + 4 + 48 * i
    if u32(at) == thread:
        stack = (u64(at + 24), u32(at + 32), u32(at + 36))
assert stack
kernel = c.WinDLL('kernel32', use_last_error=True)
kernel.VirtualAlloc.argtypes = [c.c_void_p, c.c_size_t, c.c_uint32, c.c_uint32]
kernel.VirtualAlloc.restype = c.c_void_p
kernel.GetModuleHandleW.argtypes = [c.c_wchar_p]
kernel.GetModuleHandleW.restype = c.c_void_p
unwind = c.WinDLL('ntdll').RtlVirtualUnwind
unwind.argtypes = [c.c_uint32, c.c_uint64, c.c_uint64, c.c_void_p, c.c_void_p,
                   c.POINTER(c.c_void_p), c.POINTER(c.c_uint64), c.c_void_p]
unwind.restype = c.c_void_p

start, size, offset = stack
low = start & ~0xffff
mapped = kernel.VirtualAlloc(low, start + size - low, 0x3000, 4)
assert mapped == low, ('stack mapping unavailable', c.get_last_error())
c.memmove(start, data[offset:offset + size], size)
cache = {}

def image(module):
    base, end, filename = module
    if base in cache:
        return cache[base]
    pe = pefile.PE(filename, fast_load=True)
    pe.parse_data_directories([0, 3])
    if kernel.GetModuleHandleW(Path(filename).name) != base:
        mapped = kernel.VirtualAlloc(base, pe.OPTIONAL_HEADER.SizeOfImage, 0x3000, 4)
        if mapped != base:
            raise RuntimeError('image mapping unavailable: ' + filename)
        content = pe.get_memory_mapped_image()
        c.memmove(base, content, len(content))
    entries = getattr(pe, 'DIRECTORY_ENTRY_EXCEPTION', [])
    exports = sorted((s.address, s.name.decode('ascii', 'replace'))
                     for s in getattr(getattr(pe, 'DIRECTORY_ENTRY_EXPORT', None), 'symbols', []) if s.name)
    cache[base] = pe, entries, exports
    return cache[base]

print('Dump:', path.name, 'thread:', thread, 'exception:', hex(u32(streams[6][0] + 8)), flush=True)
for frame in range(80):
    rip, rsp = reg(248), reg(152)
    if not start <= rsp <= start + size - 8:
        print('STOP: stack bytes unavailable', flush=True)
        break
    index = bisect.bisect_right(bases, rip) - 1
    if index < 0 or rip >= modules[index][1]:
        print(frame, 'unmapped/JIT frame', hex(rip), flush=True)
        break
    module = modules[index]
    base, _, filename = module
    pe, entries, exports = image(module)
    rva = rip - base
    j = bisect.bisect_right([e[0] for e in exports], rva) - 1
    symbol = (' ' + exports[j][1] + '+0x%x' % (rva - exports[j][0])) if j >= 0 and rva - exports[j][0] < 0x2000 else ''
    print(frame, Path(filename).name + '+0x%x' % rva + symbol, 'rsp=' + hex(rsp), flush=True)
    entry = next((e for e in entries if e.struct.BeginAddress <= rva < e.struct.EndAddress), None)
    if entry:
        handler, establisher = c.c_void_p(), c.c_uint64()
        unwind(0, base, rip, base + pe.get_rva_from_offset(entry.struct.get_file_offset()),
               context, c.byref(handler), c.byref(establisher), None)
    else:
        struct.pack_into('<Q', context, 248, c.c_uint64.from_address(rsp).value)
        struct.pack_into('<Q', context, 152, rsp + 8)
    if (rip, rsp) == (reg(248), reg(152)):
        print('STOP: unwinder made no progress', flush=True)
        break
