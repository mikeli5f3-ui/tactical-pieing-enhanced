"""Build a tiny x64 Lua-loadable Windows bridge using only Python stdlib.

The bridge has no UE/UE4SS/Lua ABI dependency: its luaopen entry returns zero
Lua results and launches the sibling MovingLeanAuto.exe via CreateProcessW.
DllMain only records HMODULE. No work, process launch, or patch occurs there.
"""
from pathlib import Path
import struct
import sys

def align(n, a): return (n+a-1)//a*a
def u32(n): return struct.pack('<I', n)
code=bytearray(); labels={}; fixups=[]
def emit(s): code.extend(bytes.fromhex(s))
def label(s): labels[s]=len(code)
def relative(op, name, section='code'):
    emit(op); pos=len(code); code.extend(b'\0'*4); fixups.append((pos,name,section))

label('DllMain')
emit('83 fa 01'); relative('0f 85','entry_done')
relative('48 89 0d','module','data')
label('entry_done'); emit('b8 01 00 00 00 c3')
while len(code)%16: emit('90')
label('luaopen')
emit('53 56 57 48 81 ec 00 01 00 00')
emit('48 8d 7c 24 60 31 c0 b9 12 00 00 00 f3 48 ab')
emit('c7 44 24 60 68 00 00 00 c7 84 24 9c 00 00 00 01 00 00 00')
relative('48 8b 0d','module','data')
relative('48 8d 15','path','data')
emit('41 b8 00 10 00 00'); relative('ff 15','GetModuleFileNameW','rdata')
emit('85 c0'); relative('0f 84','done')
emit('3d dc 0f 00 00'); relative('0f 83','done')
emit('89 c1'); relative('48 8d 35','path','data')
label('scan'); emit('ff c9'); relative('0f 88','done')
emit('66 83 3c 4e 5c'); relative('0f 85','scan')
emit('48 8d 7c 4e 02'); relative('48 8d 35','suffix','rdata')
emit('b9'); code.extend(u32(len('MovingLeanAuto.exe')+1)); emit('f3 66 a5')
relative('48 8d 0d','path','data')
emit('31 d2 45 31 c0 45 31 c9 31 c0')
emit('48 89 44 24 20 c7 44 24 28 00 00 00 08 48 89 44 24 30 48 89 44 24 38')
emit('48 8d 44 24 60 48 89 44 24 40 48 8d 84 24 d0 00 00 00 48 89 44 24 48')
relative('ff 15','CreateProcessW','rdata')
emit('85 c0'); relative('0f 84','done')
emit('48 8b 8c 24 d0 00 00 00'); relative('ff 15','CloseHandle','rdata')
emit('48 8b 8c 24 d8 00 00 00'); relative('ff 15','CloseHandle','rdata')
label('done'); emit('31 c0 48 81 c4 00 01 00 00 5f 5e 5b c3')

rdata=bytearray(); rd={}
def reserve(name,size,alignment=1):
    rdata.extend(b'\0'*(align(len(rdata),alignment)-len(rdata)))
    rd[name]=len(rdata); rdata.extend(b'\0'*size); return rd[name]
def string(name, value):
    p=reserve(name,len(value)); rdata[p:p+len(value)]=value

reserve('export',40,4); reserve('eat',4,4); reserve('enames',4,4); reserve('ordinals',2,2)
string('dllname',b'MovingLeanBridge.dll\0'); string('exportname',b'luaopen_MovingLeanBridge\0')
reserve('imports',40,8); reserve('int',32,8); reserve('iat',32,8)
string('kernel',b'KERNEL32.dll\0')
imports=['GetModuleFileNameW','CreateProcessW','CloseHandle']
for name in imports:
    p=reserve('hint_'+name,len(name)+3,2); rdata[p:p+len(name)+3]=b'\0\0'+name.encode()+b'\0'
string('suffix',('MovingLeanAuto.exe\0').encode('utf-16le'))
reserve('reloc',12,4)
data=bytearray(8+4096*2); dat={'module':0,'path':8}
text_rva, rd_rva, data_rva=0x1000,0x2000,0x3000
for i,name in enumerate(imports): rd[name]=rd['iat']+8*i
for pos,name,section in fixups:
    target={'code':lambda:text_rva+labels[name], 'rdata':lambda:rd_rva+rd[name], 'data':lambda:data_rva+dat[name]}[section]()
    code[pos:pos+4]=struct.pack('<i',target-(text_rva+pos+4))
for i,name in enumerate(imports):
    struct.pack_into('<Q',rdata,rd['int']+8*i,rd_rva+rd['hint_'+name])
    struct.pack_into('<Q',rdata,rd['iat']+8*i,rd_rva+rd['hint_'+name])
    rd[name]=rd['iat']+8*i
struct.pack_into('<IIHHIIIIIII',rdata,rd['export'],0,0,0,0,rd_rva+rd['dllname'],1,1,1,rd_rva+rd['eat'],rd_rva+rd['enames'],rd_rva+rd['ordinals'])
struct.pack_into('<I',rdata,rd['eat'],text_rva+labels['luaopen'])
struct.pack_into('<I',rdata,rd['enames'],rd_rva+rd['exportname'])
struct.pack_into('<IIIII',rdata,rd['imports'],rd_rva+rd['int'],0,0,rd_rva+rd['kernel'],rd_rva+rd['iat'])
struct.pack_into('<IIHH',rdata,rd['reloc'],text_rva,12,0,0)
assert len(code)<=0x1000 and len(rdata)<=0x1000

file_align=0x200
header=bytearray(0x200); header[:2]=b'MZ'; struct.pack_into('<I',header,0x3c,0x80)
header[0x80:0x84]=b'PE\0\0'
struct.pack_into('<HHIIIHH',header,0x84,0x8664,3,0,0,0,240,0x2022)
opt=0x98
struct.pack_into('<HBBIIIIIQII',header,opt,0x20b,14,0,align(len(code),512),align(len(rdata),512)+align(len(data),512),0,text_rva+labels['DllMain'],text_rva,0x180000000,0x1000,512)
struct.pack_into('<HHHHHH',header,opt+40,6,0,0,0,6,0)
struct.pack_into('<IIIIHH',header,opt+52,0,align(data_rva+len(data),4096),512,0,2,0x160)
struct.pack_into('<QQQQII',header,opt+72,0x100000,0x1000,0x100000,0x1000,0,16)
for index,rva,size in [(0,rd_rva+rd['export'],rd['imports']),(1,rd_rva+rd['imports'],40),(5,rd_rva+rd['reloc'],12),(12,rd_rva+rd['iat'],32)]:
    struct.pack_into('<II',header,opt+112+index*8,rva,size)
raw=512
chunks=[]
for i,(name,rva,content,flags) in enumerate([(b'.text',text_rva,code,0x60000020),(b'.rdata',rd_rva,rdata,0x40000040),(b'.data',data_rva,data,0xc0000040)]):
    size=align(len(content),512)
    struct.pack_into('<8sIIIIIIHHI',header,opt+240+i*40,name,len(content),rva,size,raw,0,0,0,0,flags)
    chunks.append(content+b'\0'*(size-len(content))); raw+=size
target=Path(sys.argv[1]); target.parent.mkdir(parents=True,exist_ok=True)
target.write_bytes(header+b''.join(chunks))
print(f'Built {target}: {target.stat().st_size} bytes; Lua entry RVA {text_rva+labels["luaopen"]:#x}')
