{$define DEBUG}

{$ifndef DEBUG}
	procedure A;
	begin
	end;
{$else} 
    procedure B;
	begin
	end;
{$endif}
  
begin
  {$ifndef DEBUG}
    writeln('Имя DEBUG не определено');
  {$else} 
    writeln('Имя DEBUG определено');
  {$endif}
end.