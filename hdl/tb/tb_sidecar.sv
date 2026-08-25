`timescale 1ns/1ps
`default_nettype none

module tb_sidecar #(
    parameter integer W=4
);
    localparam integer LIMIT=(1<<W);
    localparam integer MASK=LIMIT-1;
    localparam integer T2=(W==4)?3:((W==6)?5:7);
    localparam integer T3=(W==4)?2:((W==6)?3:5);
    localparam integer T5=(W==4)?1:((W==6)?2:3);
    localparam integer T7=(W==4)?1:2;
    reg [W-1:0] magnitude;
    wire enc_zero,enc_valid;
    wire [T2-1:0] enc2; wire [T3-1:0] enc3;
    wire [T5-1:0] enc5; wire [T7-1:0] enc7;
    reg sidecar_valid;
    reg [T2-1:0] t2; reg [T3-1:0] t3;
    reg [T5-1:0] t5; reg [T7-1:0] t7;
    reg [1:0] operation,prime_select;
    wire [W-1:0] magnitude_y;
    wire valid_y;
    wire [T2-1:0] y2; wire [T3-1:0] y3;
    wire [T5-1:0] y5; wire [T7-1:0] y7;
    wire predicate,rejected,overflow;
    integer n,pindex,p,expected_n;
    integer v2,v3,v5,v7;

    pa_cold_encode_s4 #(.W(W),.T2(T2),.T3(T3),.T5(T5),.T7(T7)) encoder(
        .magnitude(magnitude),.zero(enc_zero),.valid(enc_valid),.t2(enc2),.t3(enc3),.t5(enc5),.t7(enc7));
    pa_bin_vsc_s4 #(.W(W),.T2(T2),.T3(T3),.T5(T5),.T7(T7)) sidecar(
        .magnitude(magnitude),.sidecar_valid(sidecar_valid),.t2(t2),.t3(t3),.t5(t5),.t7(t7),
        .operation(operation),.prime_select(prime_select),.magnitude_y(magnitude_y),.valid_y(valid_y),
        .y2(y2),.y3(y3),.y5(y5),.y7(y7),.predicate(predicate),.rejected(rejected),.overflow(overflow));

    function automatic integer valuation(input integer value,input integer prime);
        integer q,count;
        begin
            q=value; count=0;
            if(q!=0) begin
                while((q%prime)==0) begin q=q/prime; count=count+1; end
            end
            valuation=count;
        end
    endfunction
    function automatic integer mask_for(input integer exponent);
        mask_for=(1<<exponent)-1;
    endfunction

    task automatic assert_state(input integer expected_value,input integer expected_valid);
        integer e2,e3,e5,e7;
        begin
            if(magnitude_y!==expected_value[W-1:0]) $fatal(1,"sidecar magnitude W=%0d n=%0d op=%0d p=%0d got=%0d expected=%0d",W,n,operation,p,magnitude_y,expected_value);
            if(valid_y!==expected_valid) $fatal(1,"sidecar valid W=%0d n=%0d op=%0d p=%0d",W,n,operation,p);
            if(expected_valid) begin
                e2=(expected_value==0)?T2:valuation(expected_value,2);
                e3=(expected_value==0)?T3:valuation(expected_value,3);
                e5=(expected_value==0)?T5:valuation(expected_value,5);
                e7=(expected_value==0)?T7:valuation(expected_value,7);
                if(y2!==mask_for(e2)||y3!==mask_for(e3)||y5!==mask_for(e5)||y7!==mask_for(e7)) $fatal(1,"sidecar thresholds W=%0d n=%0d op=%0d p=%0d",W,n,operation,p);
            end
        end
    endtask

    initial begin
        magnitude=0;sidecar_valid=0;t2=0;t3=0;t5=0;t7=0;operation=0;prime_select=0;
        for(n=0;n<LIMIT;n=n+1) begin
            magnitude=n[W-1:0]; #1;
            v2=(n==0)?T2:valuation(n,2);
            v3=(n==0)?T3:valuation(n,3);
            v5=(n==0)?T5:valuation(n,5);
            v7=(n==0)?T7:valuation(n,7);
            if(enc_zero!==(n==0)||enc_valid!==1'b1) $fatal(1,"cold encoder tags W=%0d n=%0d",W,n);
            if(enc2!==mask_for(v2)||enc3!==mask_for(v3)||enc5!==mask_for(v5)||enc7!==mask_for(v7)) $fatal(1,"cold encoder thresholds W=%0d n=%0d",W,n);
            sidecar_valid=enc_valid;t2=enc2;t3=enc3;t5=enc5;t7=enc7;
            for(pindex=0;pindex<4;pindex=pindex+1) begin
                prime_select=pindex[1:0];
                case(pindex) 0:p=2;1:p=3;2:p=5;default:p=7; endcase
                operation=0;#1;
                if(rejected||predicate!==((n==0)||((n%p)==0))) $fatal(1,"sidecar query W=%0d n=%0d p=%0d",W,n,p);
                assert_state(n,1);

                operation=1;#1;
                expected_n=n*p;
                if(expected_n>MASK) begin
                    if(!rejected) $fatal(1,"sidecar multiply rejection W=%0d n=%0d p=%0d",W,n,p);
                    assert_state(n,1);
                end else begin
                    if(rejected||overflow) $fatal(1,"sidecar multiply false rejection W=%0d n=%0d p=%0d",W,n,p);
                    assert_state(expected_n,1);
                end
                if(overflow!==(expected_n>MASK)) $fatal(1,"sidecar overflow W=%0d n=%0d p=%0d",W,n,p);

                operation=2;#1;
                if(n==0||(n%p)==0) begin
                    if(rejected) $fatal(1,"sidecar cancel false rejection W=%0d n=%0d p=%0d",W,n,p);
                    assert_state((n==0)?0:(n/p),1);
                end else begin
                    if(!rejected) $fatal(1,"sidecar cancel missing rejection W=%0d n=%0d p=%0d",W,n,p);
                    assert_state(n,1);
                end
            end
            operation=3;#1;
            if(rejected||valid_y||magnitude_y!==magnitude||y2!==t2||y3!==t3||y5!==t5||y7!==t7) $fatal(1,"sidecar invalidate W=%0d n=%0d",W,n);
        end

        // Malformed-but-tagged metadata cannot drive a query or update.
        magnitude=3;sidecar_valid=1;t2={{(T2-2){1'b0}},2'b10};t3=0;t5=0;t7=0;operation=0;prime_select=0;#1;
        if(!rejected||predicate) $fatal(1,"malformed sidecar query accepted W=%0d",W);
        operation=1;#1;
        if(!rejected||magnitude_y!==magnitude||y2!==t2) $fatal(1,"malformed sidecar update not atomic W=%0d",W);

        $display("PASS tb_sidecar W=%0d magnitudes=%0d prime_ops=%0d",W,LIMIT,LIMIT*4);
        $finish;
    end
endmodule

`default_nettype wire
